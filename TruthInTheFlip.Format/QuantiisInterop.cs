using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace TruthInTheFlip.Format;

public static class QuantisInterop
{
    /* Dev note:
     * Although unveiled on the branch, this code is still under active development and pending testing.
     * The Quantis library is a proprietary product, and its API may change without notice.
     * This code is provided as-is and without warranty.
     */
    
    public enum DeviceType
    {
        // Standard device types (check the Quantis .h header for your specific version)
        PCI = 0,
        USB = 1,
        PCIe = 2
    }

    public interface ISimpleQuant
    {
        int Read(byte[] buffer, int size);
        void AssertReady();
        bool SourceEntropyMode(bool enable = true);
        
        bool? CurrentSourceEntropyMode { get; set; }
    }

    //It is unclear if the final separation will be Windows vs Linux. Or Modern vs Legacy or their combination,
    // but structurally this is how differences will be tracked
    
    public class WindowsQuant : ISimpleQuant
    {
        public DeviceType DeviceType { get; }
        public int DeviceNumber { get; }
        
        public WindowsQuant(int deviceNumber, DeviceType deviceType)
        {
            DeviceType = deviceType;
            DeviceNumber = deviceNumber;
        }
        
        public class Imports
        {
            // The core C API function to pull full entropy raw bytes
            [DllImport("Quantis", CallingConvention = CallingConvention.Cdecl)]
            public static extern int QuantisRead(DeviceType deviceType, int deviceNumber, byte[] buffer, int size);

            // Hardware polling - critical for guaranteeing true physical randomness
            [DllImport("Quantis", CallingConvention = CallingConvention.Cdecl)]
            public static extern int QuantisGetBoardStatus(DeviceType deviceType, int deviceNumber);

            // Bypasses the embedded NIST 800-90 DRBG / von Neumann extractor.
            // (Note: Verify the exact naming in your specific driver's quantis.h header)
            [DllImport("Quantis", CallingConvention = CallingConvention.Cdecl)]
            public static extern int QuantisDisableExtractor(DeviceType deviceType, int deviceNumber);
        }

        public int Read(byte[] buffer, int size)
        {
            return Imports.QuantisRead(DeviceType, DeviceNumber, buffer, size);
        }

        public void AssertReady()
        {
            int status = Imports.QuantisGetBoardStatus(DeviceType, DeviceNumber);
            if (status != 0)
            {
                throw new InvalidOperationException(
                    $"Quantis hardware fault (Code: {status}). True entropy stream disabled.");
            }
        }

        public bool SourceEntropyMode(bool enable = true)
        {
            if (CurrentSourceEntropyMode.HasValue &&
                CurrentSourceEntropyMode.Value != enable)
            {
                throw new InvalidOperationException(
                    $"Quantis source mode is already fixed to " +
                    $"{CurrentSourceEntropyMode.Value}, but {enable} was requested.");
            }
            
            bool rc;

            if (enable) rc = Imports.QuantisDisableExtractor(DeviceType, DeviceNumber) == 0;
            else rc = true;
            
            if (rc) CurrentSourceEntropyMode = enable;
            
            return rc;
        }

        public bool? CurrentSourceEntropyMode { get; set; }
    }

    public class LinuxQuant : ISimpleQuant
    {
        public DeviceType DeviceType { get; }
        public int DeviceNumber { get; }
        
        public LinuxQuant(int deviceNumber, DeviceType deviceType)
        {
            DeviceType = deviceType;
            DeviceNumber = deviceNumber;
        }
        
        public class Imports
        {
            // The core C API function to pull full entropy raw bytes
            [DllImport("quantis", CallingConvention = CallingConvention.Cdecl)]
            public static extern int QuantisRead(DeviceType deviceType, int deviceNumber, byte[] buffer, int size);

            // Hardware polling - critical for guaranteeing true physical randomness
            [DllImport("quantis", CallingConvention = CallingConvention.Cdecl)]
            public static extern int QuantisGetBoardStatus(DeviceType deviceType, int deviceNumber);

            // Bypasses the embedded NIST 800-90 DRBG / von Neumann extractor.
            // (Note: Verify the exact naming in your specific driver's quantis.h header)
            [DllImport("quantis", CallingConvention = CallingConvention.Cdecl)]
            public static extern int QuantisDisableExtractor(DeviceType deviceType, int deviceNumber);
        }

        public int Read(byte[] buffer, int size)
        {
            return Imports.QuantisRead(DeviceType, DeviceNumber, buffer, size);
        }

        public void AssertReady()
        {
            int status = Imports.QuantisGetBoardStatus(DeviceType, DeviceNumber);
            if (status != 0)
            {
                throw new InvalidOperationException(
                    $"Quantis hardware fault (Code: {status}). True entropy stream disabled.");
            }
        }

        public bool SourceEntropyMode(bool enable = true)
        {
            if (CurrentSourceEntropyMode.HasValue &&
                CurrentSourceEntropyMode.Value != enable)
            {
                throw new InvalidOperationException(
                    $"Quantis source mode is already fixed to " +
                    $"{CurrentSourceEntropyMode.Value}, but {enable} was requested.");
            }
            
            bool rc;

            if (enable) rc = Imports.QuantisDisableExtractor(DeviceType, DeviceNumber) == 0;
            else rc = true;
            
            if (rc) CurrentSourceEntropyMode = enable;
            
            return rc;
        }

        public bool? CurrentSourceEntropyMode { get; set; }
    }
    
    public static ConcurrentDictionary<(int,DeviceType), ISimpleQuant> devices = new ConcurrentDictionary<(int,DeviceType), ISimpleQuant>();
    
    public static Func<Action<byte[]>> quantisFactory(bool enforcePureEntropy, int deviceNumber, DeviceType deviceType)
    {
        if (!devices.TryGetValue((deviceNumber, deviceType), out var device))
        {
            if (OperatingSystem.IsWindows())
                device = new WindowsQuant(deviceNumber, deviceType);
            else
                device = new LinuxQuant(deviceNumber, deviceType);
            
            devices[(deviceNumber, deviceType)] = device;
        }

        return () =>
        {
            device.AssertReady();
            if (!device.SourceEntropyMode(enforcePureEntropy))
                throw new InvalidOperationException("requested source entropy mode not delivered");
            if (enforcePureEntropy)
            {
                Console.WriteLine("Quantis QRNG Initialized: PURE ENTROPY SOURCE MODE ACTIVE.");
            }
            else
            {
                Console.WriteLine("Quantis QRNG Initialized: NIST DRBG Mode Active.");
            }

            return array =>
            {
                int bytesRead = device.Read(array, array.Length);

                if (bytesRead != array.Length)
                {
                    throw new InvalidOperationException(
                        $"Quantis read underflow: requested {array.Length} bytes, " +
                        $"received {bytesRead}.");
                }
            };
        };
    }
}