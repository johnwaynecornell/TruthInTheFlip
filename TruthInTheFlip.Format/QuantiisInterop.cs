using System;
using System.Collections.Generic;
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

        protected abstract nint ReadNative(
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

                if ((modulesStatus & modulesMask) != modulesMask)
                {
                    int unavailableMask = modulesMask & ~modulesStatus;

                    throw new InvalidOperationException(
                        $"Quantis {DeviceType} device {DeviceNumber} " +
                        $"has unavailable entropy modules. " +
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

        private static Exception CreateNativeException(
            string operation,
            int errorCode)
        {
            return new InvalidOperationException(
                $"{operation} failed with Quantis error code {errorCode}.");
        }
    }

    private sealed class ModernQuant : SimpleQuantBase
    {
        public ModernQuant(uint deviceNumber, DeviceType deviceType)
            : base(deviceNumber, deviceType)
        {
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
            internal static extern nint QuantisRead(
                DeviceType deviceType,
                uint deviceNumber,
                byte[] buffer,
                nint size);
            
            [DllImport(
                "Quantis",
                EntryPoint = "QuantisReadRaw",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern nint QuantisReadRaw(
                DeviceType deviceType,
                uint deviceNumber,
                byte[] buffer,
                nint size);
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

        
        protected override nint ReadNative(
            DeviceType deviceType,
            uint deviceNumber,
            byte[] buffer,
            nint size, bool sourceEntropy) =>
            sourceEntropy ? Imports.QuantisRead(deviceType, deviceNumber, buffer, size) : Imports.QuantisRead(deviceType, deviceNumber, buffer, size);
    }
    
    private static readonly Dictionary<
        (uint Number, DeviceType Type),
        ISimpleQuant> Devices = new();

    public static Func<Action<byte[]>> QuantisFactory(
        bool enforcePureEntropy,
        uint deviceNumber,
        DeviceType deviceType)
    {
        ISimpleQuant device;

        
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
            
            Console.WriteLine($"Quantis QRNG initialized.  {deviceType} Dev{deviceNumber}{ ((!enforcePureEntropy) ? " not" : "")} using source entropy");
            Console.WriteLine("Source Entropy gimped using QuantisRead until update");
                
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