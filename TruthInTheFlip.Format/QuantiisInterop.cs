using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TruthInTheFlip.Format;

public static class QuantisInterop
{
    /*
     * This code remains under active development and requires validation
     * against the Quantis headers distributed with the installed SDK.
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

        int Read(byte[] buffer, nuint size);

        void AssertReady();

        bool SetSourceEntropyMode(bool ? enable = true);

        bool? CurrentSourceEntropyMode { get; }
    }

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

        public bool? CurrentSourceEntropyMode { get; private set; }

        [DllImport(
            "Quantis",
            EntryPoint = "QuantisCount",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int QuantisCountWindows(DeviceType deviceType);
        
        [DllImport(
            "quantis",
            EntryPoint = "QuantisCount",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int QuantisCountLinux(DeviceType deviceType);
        
        public static int CountDevices(DeviceType deviceType)
        {
            if (OperatingSystem.IsWindows())
                return QuantisCountWindows(deviceType);

            if (OperatingSystem.IsLinux())
                return QuantisCountLinux(deviceType);

            throw new PlatformNotSupportedException(
                "Quantis device enumeration currently supports Windows and Linux.");
        }
        protected abstract int GetModulesMask(
            DeviceType deviceType,
            uint deviceNumber);

        protected abstract int GetModulesStatus(
            DeviceType deviceType,
            uint deviceNumber);

        protected abstract int GetModulesPower(
            DeviceType deviceType,
            uint deviceNumber);

        protected abstract int DisableExtractor(
            DeviceType deviceType,
            uint deviceNumber);

        protected abstract int ReadNative(
            DeviceType deviceType,
            uint deviceNumber,
            byte[] buffer,
            nuint size);

        public int Read(byte[] buffer, nuint size)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (size > (nuint)buffer.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    "The requested read size exceeds the destination buffer.");
            }

            return ReadNative(DeviceType, DeviceNumber, buffer, size);
        }

        public void AssertReady()
        {
            int count = CountDevices(DeviceType);

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

        public bool SetSourceEntropyMode(bool ? enable = true)
        {
            lock (stateLock)
            {
                if (enable != null)
                {
                    if (CurrentSourceEntropyMode.HasValue)
                    {
                        if (CurrentSourceEntropyMode.Value != enable)
                        {
                            throw new InvalidOperationException(
                                $"Quantis {DeviceType} device {DeviceNumber} " +
                                $"was already initialized with source entropy mode " +
                                $"{CurrentSourceEntropyMode.Value}.");
                        }

                        return true;
                    }
                }

                if (enable == true)
                {
                    int rc = DisableExtractor(DeviceType, DeviceNumber);

                    if (rc != 0)
                        return false;
                }

                /*
                 * When enable is false, no native operation is performed.
                 * This means: preserve the device/API default processing mode. An explicit mode set will be looked into.
                 * enable == null means the caller has relinquished control over the mode.
                 */
                CurrentSourceEntropyMode = enable;
                return true;
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

    private sealed class WindowsQuant : SimpleQuantBase
    {
        public WindowsQuant(uint deviceNumber, DeviceType deviceType)
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

            /*
             * This import is still provisional. Verify its existence and
             * signature against the SDK installed with the device.
             */
            [DllImport(
                "Quantis",
                EntryPoint = "QuantisDisableExtractor",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisDisableExtractor(
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
                nuint size);
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

        protected override int DisableExtractor(
            DeviceType deviceType,
            uint deviceNumber) =>
            Imports.QuantisDisableExtractor(deviceType, deviceNumber);

        protected override int ReadNative(
            DeviceType deviceType,
            uint deviceNumber,
            byte[] buffer,
            nuint size) =>
            Imports.QuantisRead(deviceType, deviceNumber, buffer, size);
    }

    private sealed class LinuxQuant : SimpleQuantBase
    {
        public LinuxQuant(uint deviceNumber, DeviceType deviceType)
            : base(deviceNumber, deviceType)
        {
        }

        private static class Imports
        {
            [DllImport(
                "quantis",
                EntryPoint = "QuantisGetModulesMask",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisGetModulesMask(
                DeviceType deviceType,
                uint deviceNumber);

            [DllImport(
                "quantis",
                EntryPoint = "QuantisGetModulesStatus",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisGetModulesStatus(
                DeviceType deviceType,
                uint deviceNumber);

            [DllImport(
                "quantis",
                EntryPoint = "QuantisGetModulesPower",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisGetModulesPower(
                DeviceType deviceType,
                uint deviceNumber);

            /*
             * Provisional until verified against the installed SDK.
             */
            [DllImport(
                "quantis",
                EntryPoint = "QuantisDisableExtractor",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisDisableExtractor(
                DeviceType deviceType,
                uint deviceNumber);

            [DllImport(
                "quantis",
                EntryPoint = "QuantisRead",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int QuantisRead(
                DeviceType deviceType,
                uint deviceNumber,
                byte[] buffer,
                nuint size);
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

        protected override int DisableExtractor(
            DeviceType deviceType,
            uint deviceNumber) =>
            Imports.QuantisDisableExtractor(deviceType, deviceNumber);

        protected override int ReadNative(
            DeviceType deviceType,
            uint deviceNumber,
            byte[] buffer,
            nuint size) =>
            Imports.QuantisRead(deviceType, deviceNumber, buffer, size);
    }

    private static readonly object DevicesLock = new();

    private static readonly Dictionary<
        (uint Number, DeviceType Type),
        ISimpleQuant> Devices = new();

    public static Func<Action<byte[]>> QuantisFactory(
        bool enforcePureEntropy,
        uint deviceNumber,
        DeviceType deviceType)
    {
        ISimpleQuant device;

        lock (DevicesLock)
        {
            if (!Devices.TryGetValue(
                    (deviceNumber, deviceType),
                    out device!))
            {
                if (OperatingSystem.IsWindows())
                {
                    device = new WindowsQuant(deviceNumber, deviceType);
                }
                else if (OperatingSystem.IsLinux())
                {
                    device = new LinuxQuant(deviceNumber, deviceType);
                }
                else
                {
                    throw new PlatformNotSupportedException(
                        "Quantis interop currently supports Windows and Linux.");
                }

                Devices[(deviceNumber, deviceType)] = device;
            }
        }

        return () =>
        {
            device.AssertReady();

            if (!device.SetSourceEntropyMode(enforcePureEntropy))
            {
                throw new InvalidOperationException(
                    "The requested Quantis entropy-source mode was not delivered.");
            }

            Console.WriteLine(
                enforcePureEntropy
                    ? "Quantis QRNG initialized: source entropy mode active."
                    : "Quantis QRNG initialized: default processing mode active.");

            return buffer =>
            {
                ArgumentNullException.ThrowIfNull(buffer);

                int bytesRead = device.Read(
                    buffer,
                    (nuint)buffer.Length);

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