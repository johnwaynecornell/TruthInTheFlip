using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TruthInTheFlip.Format;

public static class QuantisInterop
{
    /*
     * This code remains under active development and requires validation
     *  against the Quantis headers distributed with the installed SDK.
     *  Validation has begun. Updates soon.
     */
    
    /*
     * In order to allow compilation without binary dependencies, DllImport is used
     */
    

    public enum DeviceType
    {
        // The Quantis API groups PCI and PCI Express under the same value.
        PCI = 1,
        USB = 2
    }

    public interface ISimpleQuant
    {
        DeviceType DeviceType { get; }
        uint DeviceNumber { get; }

        nint Read(byte[] buffer, nint size, bool sourceEntropy);

        void AssertReady();
    }

    [DllImport(
        "Quantis",
        EntryPoint = "QuantisCount",
        CallingConvention = CallingConvention.Cdecl)]
    public static extern int QuantisCount(DeviceType deviceType);
    
    private abstract class SimpleQuantBase : ISimpleQuant
    {
        private readonly object stateLock = new();

        protected SimpleQuantBase(uint deviceNumber, DeviceType deviceType)
        {
            DeviceNumber = deviceNumber;
            DeviceType = deviceType;
        }

        public DeviceType DeviceType { get; }

        public uint DeviceNumber { get; }
        
        protected abstract int GetModulesMask(
            DeviceType deviceType,
            uint deviceNumber);

        protected abstract int GetModulesStatus(
            DeviceType deviceType,
            uint deviceNumber);

        protected abstract int GetModulesPower(
            DeviceType deviceType,
            uint deviceNumber);

        protected abstract int ReadNative(
            DeviceType deviceType,
            uint deviceNumber,
            byte[] buffer,
            nint size,
            bool sourceEntropy);

        public nint Read(byte[] buffer, nint size, bool sourceEntropy)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (size > (nint)buffer.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    "The requested read size exceeds the destination buffer.");
            }

            nint bytesRead = ReadNative(DeviceType, DeviceNumber, buffer, size, sourceEntropy);
            if (bytesRead < 0)
            {
                throw CreateNativeException(
                    "QuantisRead",
                    (int)bytesRead);
            }
            
            if (bytesRead != size)
            {
                throw new InvalidOperationException($"QRNG random read failed. Expected {size} bytes, got {bytesRead}.");
            }

            return bytesRead;
        }

        public void AssertReady()
        {
            try
            {

                int count = QuantisCount(DeviceType);

                if (count < 0)
                {
                    throw CreateNativeException(
                        "QuantisCount",
                        count);
                }

                if (DeviceNumber >= (uint)count)
                {
                    throw new InvalidOperationException(
                        $"Quantis {DeviceType} device {DeviceNumber} is unavailable. " +
                        $"{count} device(s) of that type were detected.");
                }

                int modulesMask = GetModulesMask(DeviceType, DeviceNumber);

                if (modulesMask < 0)
                {
                    throw CreateNativeException(
                        "QuantisGetModulesMask",
                        modulesMask);
                }

                if (modulesMask == 0)
                {
                    throw new InvalidOperationException(
                        $"Quantis {DeviceType} device {DeviceNumber} " +
                        "does not report any installed entropy modules.");
                }

                int modulesPower = GetModulesPower(DeviceType, DeviceNumber);

                if (modulesPower < 0)
                {
                    throw CreateNativeException(
                        "QuantisGetModulesPower",
                        modulesPower);
                }

                if (modulesPower == 0)
                {
                    throw new InvalidOperationException(
                        $"Quantis {DeviceType} device {DeviceNumber} " +
                        "reports that its entropy modules are not powered.");
                }

                int modulesStatus = GetModulesStatus(DeviceType, DeviceNumber);

                if (modulesStatus < 0)
                {
                    throw CreateNativeException(
                        "QuantisGetModulesStatus",
                        modulesStatus);
                }

                if (modulesStatus == 0)
                {
                    throw new InvalidOperationException(
                        $"Quantis {DeviceType} device {DeviceNumber} " +
                        "has no enabled and functional entropy modules.");
                }
                
                int unavailableMask = modulesMask & ~modulesStatus;
                if (unavailableMask != 0)
                {
                    Console.Error.WriteLine(
                        $"Quantis warning: {DeviceType} device {DeviceNumber} " +
                        $"is operating with a partial module set. " +
                        $"Installed mask: 0x{modulesMask:X}; " +
                        $"functional mask: 0x{modulesStatus:X}; " +
                        $"unavailable mask: 0x{unavailableMask:X}.");
                }
            }
            catch (DllNotFoundException except)
            {
                throw new InvalidOperationException("Quantis Dll not available. If you are using Quantis, please ensure your platform dll (libquantis.so or Quantis.dll) is available on path or next to binary", except);
            }
        }

        protected static Exception CreateNativeException(
            string operation,
            int errorCode)
        {
            return new InvalidOperationException(
                $"{operation} failed with Quantis error code {errorCode}.");
        }
    }

    private sealed class ModernQuant : SimpleQuantBase , IDisposable
    {
        public IntPtr deviceHandle;
        private readonly object readLock = new();
        private bool disposed;
        
        public ModernQuant(uint deviceNumber, DeviceType deviceType)
            : base(deviceNumber, deviceType)
        {
            int rc = Imports.QuantisOpen(deviceType, deviceNumber, out deviceHandle);
            if (rc != 0)
            {
                throw CreateNativeException("QuantisOpen", rc);
            }
        }

        private static class Imports
        {
            [DllImport(
                "Quantis",
                EntryPoint = "QuantisGetModulesMask",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisGetModulesMask(
                DeviceType deviceType,
                uint deviceNumber);

            [DllImport(
                "Quantis",
                EntryPoint = "QuantisGetModulesStatus",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisGetModulesStatus(
                DeviceType deviceType,
                uint deviceNumber);

            [DllImport(
                "Quantis",
                EntryPoint = "QuantisGetModulesPower",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisGetModulesPower(
                DeviceType deviceType,
                uint deviceNumber);

            [DllImport(
                "Quantis",
                EntryPoint = "QuantisRead",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisRead(
                DeviceType deviceType,
                uint deviceNumber,
                byte[] buffer,
                nint size);
            
            [DllImport(
                "Quantis",
                EntryPoint = "QuantisOpen",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisOpen(
                DeviceType deviceType,
                uint deviceNumber, out IntPtr deviceHandle);

            [DllImport(
                "Quantis",
                EntryPoint = "QuantisClose",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisClose(IntPtr deviceHandle);
            
            [DllImport(
                "Quantis",
                EntryPoint = "QuantisReadHandled",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisReadHandled(
                IntPtr deviceHandle,
                byte[] buffer,
                nint size);
            
            /*
            DLL_EXPORT int QuantisOpen(QuantisDeviceType deviceType, unsigned int deviceNumber,QuantisDeviceHandle** deviceHandle);
            DLL_EXPORT void QuantisClose(QuantisDeviceHandle* deviceHandle);
            DLL_EXPORT int QuantisReadHandled(QuantisDeviceHandle* deviceHandle, void* buffer, size_t size);
            */


        }

        protected override int GetModulesMask(
            DeviceType deviceType,
            uint deviceNumber) =>
            Imports.QuantisGetModulesMask(deviceType, deviceNumber);

        protected override int GetModulesStatus(
            DeviceType deviceType,
            uint deviceNumber) =>
            Imports.QuantisGetModulesStatus(deviceType, deviceNumber);

        protected override int GetModulesPower(
            DeviceType deviceType,
            uint deviceNumber) =>
            Imports.QuantisGetModulesPower(deviceType, deviceNumber);
        
        protected long entropyOffset = 0;
        protected int entropyBit = 0;
        protected byte[] entropyBuffer = null;
        protected int entropyLength;

        public const nint QUANTIS_MAX_READ_SIZE = (16 * 1024 * 1024);
        
        protected override int ReadNative(
            DeviceType deviceType,
            uint deviceNumber,
            byte[] buffer,
            nint size, bool sourceEntropy) 

        {
            ObjectDisposedException.ThrowIf(disposed, this);

            lock (readLock)
            {
                nint position;
                if (sourceEntropy)
                {
                    if (size > QUANTIS_MAX_READ_SIZE)
                    {
                        byte[] entropyBuffer = new byte[QUANTIS_MAX_READ_SIZE];

                        position = 0;

                        while (position < size)
                        {
                            int readSize = Imports.QuantisReadHandled(deviceHandle, entropyBuffer,
                                Math.Min(QUANTIS_MAX_READ_SIZE, size - position));
                            Array.Copy(entropyBuffer, 0, buffer, position, readSize);
                            position += readSize;
                        }

                        return (int)size;
                    }

                    return Imports.QuantisReadHandled(deviceHandle, buffer, size);
                }

                if (entropyBuffer == null)
                {
                    entropyBuffer = new byte[32 * 1024];
                    entropyOffset = entropyBuffer.Length;
                    entropyBit = 0;
                }

                position = 0;
                int position_bit = 0;

                Func<int> getRaw = () =>
                {
                    if (entropyOffset >= entropyLength)
                    {
                        int r = Imports.QuantisReadHandled(this.deviceHandle,
                            entropyBuffer,
                            entropyBuffer.Length);

                        if (r < 0)
                            throw new InvalidOperationException(
                                $"QuantisRead failed with error code {r}.");

                        if (r == 0)
                            throw new InvalidOperationException(
                                "QuantisRead returned no data.");

                        entropyOffset = 0;
                        entropyLength = r;
                        entropyBit = 0;
                    }

                    int bit = ((entropyBuffer[entropyOffset]) >> entropyBit) & 1;
                    entropyBit++;
                    if (entropyBit == 8)
                    {
                        entropyOffset++;
                        entropyBit = 0;
                    }

                    return bit;
                };

                while (position < size)
                {
                    int bit1 = getRaw();
                    int bit2 = getRaw();

                    if (bit1 == bit2)
                        continue;

                    if (position_bit == 0)
                        buffer[position] = 0;

                    // 01 -> 0; 10 -> 1
                    if (bit1 != 0)
                        buffer[position] |= (byte)(1 << position_bit);

                    position_bit++;

                    if (position_bit == 8)
                    {
                        position++;
                        position_bit = 0;
                    }
                }

                return (int)position;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            lock (readLock)
            {
                if (deviceHandle != nint.Zero)
                {
                    Imports.QuantisClose(deviceHandle);
                    deviceHandle = nint.Zero;
                }

                disposed = true;
            }
        }
    }
    
    private static readonly Dictionary<
        (uint Number, DeviceType Type),
        ISimpleQuant> Devices = new();

    static bool? hasSDK = null;
    
    public static Assembly CallingAssembly = null;
    
    public static bool EnsureSDK()
    {
        Assembly caller = CallingAssembly;
        
        if (hasSDK != null) return hasSDK.Value;
        
        hasSDK = true;
        
        try
        {
            int count = QuantisInterop.QuantisCount(QuantisInterop.DeviceType.PCI);

            if (count < 0)
            {
                Console.Error.WriteLine("Quantis detection: 'QuantisCount' returned: " + count);
                hasSDK = false;
            } else if (count < 1)
            {
                Console.Error.WriteLine("No Quantis PCI device found");
                hasSDK = false;
            }
        }
        catch (DllNotFoundException ex)
        {
            Console.Error.WriteLine("Quantis hardware/drivers not detected. Skipping hardware entropy test.");
            Console.Error.WriteLine(
                "the IDQ library, Quantis.dll for Windows and libQuantis.so for Linux should be resolvable by OS set paths accordingly");
            hasSDK = false;
        }
        catch (BadImageFormatException ex)
        {
            nint bits = Marshal.SizeOf(typeof(nint)) * 8;
    
            Console.Error.WriteLine("The Quantis library did not load due to a BadImageFormatException.");
            Console.Error.WriteLine($"This usually means that the library does not run on {bits}-bit systems.");

            if (bits == 64 && OperatingSystem.IsWindows())
            {
                if (caller == null) caller = Assembly.GetCallingAssembly();
                
                Console.WriteLine();
                Console.Error.WriteLine("To resolve this on Windows without needing to rebuild the IDQ Quantis SDK, ensure you build TruthInTheFlip.sln for x86:");
                Console.Error.WriteLine("  dotnet build -c Release -p:Platform=x86");
                Console.Error.WriteLine("Then run with the 32-bit .NET runtime:");
                Console.Error.WriteLine($"  \"C:\\Program Files (x86)\\dotnet\\dotnet.exe\" \"{caller.Location}\"");
            }
            hasSDK = false;
        }

        return hasSDK.Value;
    }

    public static Func<Action<byte[]>> QuantisFactory(
        bool enforcePureEntropy,
        uint deviceNumber,
        DeviceType deviceType)
    {
        ISimpleQuant device;

        if (!EnsureSDK()) Environment.Exit(1);

        if (!Devices.TryGetValue(
                (deviceNumber, deviceType),
                out device!))
        {
            device = new ModernQuant(deviceNumber, deviceType);
            Devices[(deviceNumber, deviceType)] = device;
        }
        
        return () =>
        {
            device.AssertReady();

            if (enforcePureEntropy) Console.WriteLine(
                    $"Quantis QRNG instantiated.  {deviceType} Dev {deviceNumber} using source entropy");
            else Console.WriteLine(
                    $"Quantis QRNG instantiated.  {deviceType} Dev {deviceNumber} source entropy + VonNeumannWhitener");

            return buffer =>
            {
                ArgumentNullException.ThrowIfNull(buffer);

                nint bytesRead = device.Read(
                    buffer,
                    (nint)buffer.Length, enforcePureEntropy);

                if (bytesRead < 0)
                {
                    throw new InvalidOperationException(
                        $"QuantisRead failed with error code {bytesRead}.");
                }

                if (bytesRead != buffer.Length)
                {
                    throw new InvalidOperationException(
                        $"Quantis read underflow: requested {buffer.Length} " +
                        $"bytes but received {bytesRead}.");
                }
            };
        };
    }
}