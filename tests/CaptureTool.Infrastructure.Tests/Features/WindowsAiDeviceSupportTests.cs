using CaptureTool.Infrastructure.Ai;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Tests.Features;

[TestClass]
public sealed class WindowsAiDeviceSupportTests
{
    [TestMethod]
    [DataRow(Architecture.X64, Architecture.X64, true, 8UL * 1024 * 1024, true)]
    [DataRow(Architecture.X64, Architecture.X64, true, 4UL * 1024 * 1024, false)]
    [DataRow(Architecture.X64, Architecture.X64, false, 16UL * 1024 * 1024, false)]
    [DataRow(Architecture.X64, Architecture.Arm64, true, 16UL * 1024 * 1024, false)]
    [DataRow(Architecture.Arm64, Architecture.Arm64, false, 8UL * 1024 * 1024, true)]
    [DataRow(Architecture.X86, Architecture.X64, true, 16UL * 1024 * 1024, false)]
    [DataRow(Architecture.X64, Architecture.X64, true, 0UL, false)]
    public void CpuFallbackRequiresNativeArchitectureInstructionsAndInstalledMemory(
        Architecture process, Architecture system, bool avx2, ulong memory, bool expected)
    {
        Assert.AreEqual(expected, WindowsAiDeviceSupport.SupportsCpu(process, system, avx2, memory));
    }
}
