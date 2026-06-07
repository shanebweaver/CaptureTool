using CaptureTool.Infrastructure.Capture.Windows.V2;
using FluentAssertions;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests.V2;

[TestClass]
public sealed class CaptureRecorderSafeHandleTests
{
    [TestMethod]
    public void GetApiVersion_CanRunBeforeCreatingRecorderHandle()
    {
        var result = CaptureV2NativeMethods.CtCaptureV2_GetApiVersion(out var version);

        result.Should().Be((int)CaptureV2ResultCode.Success);
        version.Major.Should().Be(2);
        version.Size.Should().Be((uint)Marshal.SizeOf<CaptureV2ApiVersion>());
    }

    [TestMethod]
    public void CreateRecorder_ReturnsManagedSafeHandle()
    {
        var result = CaptureV2NativeMethods.CtCaptureV2_CreateRecorder(out var handle);

        using (handle)
        {
            result.Should().Be((int)CaptureV2ResultCode.Success);
            handle.IsInvalid.Should().BeFalse();
            handle.IsClosed.Should().BeFalse();
        }
    }

    [TestMethod]
    public void Dispose_ReleasesNativeRecorderHandleOnce()
    {
        var createResult = CaptureV2NativeMethods.CtCaptureV2_CreateRecorder(out var handle);
        createResult.Should().Be((int)CaptureV2ResultCode.Success);

        var rawHandle = handle.DangerousGetHandle();

        handle.Dispose();
        handle.Dispose();

        var destroyAfterDisposeResult = CaptureV2NativeMethods.CtCaptureV2_DestroyRecorder(rawHandle);

        handle.IsClosed.Should().BeTrue();
        destroyAfterDisposeResult.Should().Be((int)CaptureV2ResultCode.InvalidHandle);
    }
}
