using System;
using System.Runtime.InteropServices;

namespace TruthInTheFlip.Format;

/*
public static class QuantisInterop
{
    // On Linux, the IDQ library is compiled as a shared object
    private const string QuantisLib = "libquantis.so";

    // Standard device types (check the Quantis .h header for your specific version)
    public const int QUANTIS_DEVICE_PCI = 0;
    public const int QUANTIS_DEVICE_USB = 1;
    public const int QUANTIS_DEVICE_PCIe = 2;

    // The core C API function to pull full entropy raw bytes
    [DllImport(QuantisLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int QuantisRead(int deviceType, int deviceNumber, byte[] buffer, int size);

    // Hardware polling - critical for guaranteeing true physical randomness
    [DllImport(QuantisLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int QuantisGetBoardStatus(int deviceType, int deviceNumber);

    // Bypasses the embedded NIST 800-90 DRBG / von Neumann extractor.
    // (Note: Verify the exact naming in your specific driver's quantis.h header)
    [DllImport(QuantisLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int QuantisDisableExtractor(int deviceType, int deviceNumber);

    // Configurable delegate factory allowing easy swaps between PCI, PCIe, and USB,
    // while enforcing full source entropy by default.
    public static Action<byte[]> initQuantis_Linux(
        int deviceType = QuantisInterop.QUANTIS_DEVICE_PCIe,
        bool enforcePureEntropy = true)
    {
        // Assuming a single device plugged in (Device #0)
        int deviceNumber = 0;

        // Verify the physical hardware is functioning before trusting the entropy
        int status = QuantisInterop.QuantisGetBoardStatus(deviceType, deviceNumber);
        if (status != 0)
        {
            throw new InvalidOperationException(
                $"Quantis hardware fault (Code: {status}). True entropy stream disabled.");
        }

        if (enforcePureEntropy)
        {
            // Explicitly disable the NIST 800-90 DRBG post-processing.
            // This forces the device into Entropy Source Mode, streaming raw physical anomalies.
            QuantisInterop.QuantisDisableExtractor(deviceType, deviceNumber);
            Console.WriteLine("Quantis QRNG Initialized: PURE ENTROPY SOURCE MODE ACTIVE.");
        }
        else
        {
            Console.WriteLine("Quantis QRNG Initialized: NIST DRBG Mode Active.");
        }

        return (buffer) =>
        {
            // The unmanaged call pins the C# array and fills it directly from the hardware buffer
            int bytesRead = QuantisInterop.QuantisRead(deviceType, deviceNumber, buffer, buffer.Length);

            if (bytesRead != buffer.Length)
            {
                throw new Exception($"Entropy underflow. Requested {buffer.Length} bytes, received {bytesRead}.");
            }
        };
    }
} */