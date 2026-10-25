using CaptureTool.Application.Abstractions.Ai;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Ai;

/// <summary>Requirements for the packaged WinML SDK and the CPU variants selected by FoundryRuntime.</summary>
public sealed class WindowsAiDeviceSupport : IAiDeviceSupport
{
    private const uint Avx2InstructionsAvailable = 40; // PF_AVX2_INSTRUCTIONS_AVAILABLE
    public static WindowsAiDeviceSupport Current { get; } = new();
    public bool WindowsAi { get; }
    public bool FoundryCpu { get; }

    private WindowsAiDeviceSupport()
    {
        WindowsAi = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100);
        // Do not enter either AI runtime on older Windows. Use installed, not currently free, RAM.
        FoundryCpu = WindowsAi && SupportsCpu(RuntimeInformation.ProcessArchitecture,
            // Intrinsic IsSupported reflects the AOT compilation target, not just the CPU.
            RuntimeInformation.OSArchitecture, IsProcessorFeaturePresent(Avx2InstructionsAvailable),
            GetPhysicallyInstalledSystemMemory(out ulong kilobytes) ? kilobytes : 0);
    }

    public static bool SupportsCpu(Architecture process, Architecture system, bool avx2, ulong installedKilobytes) =>
        process == system && (process == Architecture.Arm64 || process == Architecture.X64 && avx2) &&
        installedKilobytes >= 8UL * 1024 * 1024;

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessorFeaturePresent(uint processorFeature);
}
