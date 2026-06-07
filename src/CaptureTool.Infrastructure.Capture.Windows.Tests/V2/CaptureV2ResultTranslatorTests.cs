using CaptureTool.Infrastructure.Capture.Windows.V2;
using FluentAssertions;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests.V2;

[TestClass]
public sealed class CaptureV2ResultTranslatorTests
{
    [TestMethod]
    public void ThrowIfFailed_WithInvalidStateNativeError_ThrowsNativeExceptionWithDetails()
    {
        CreateRecorder(out var handle);
        using (handle)
        {
            int result = CaptureV2NativeMethods.CtCaptureV2_Pause(handle);

            Action act = () => CaptureV2ResultTranslator.ThrowIfFailed(handle, result);

            act.Should().Throw<CaptureNativeException>()
                .Where(exception => exception.ResultCode == CaptureV2ResultCode.InvalidState)
                .Where(exception => exception.Component == "CaptureInteropV2Recorder")
                .Where(exception => exception.Operation == "Pause")
                .WithMessage("*paused*");
        }
    }

    [TestMethod]
    public void CreateException_WithValidationFailure_ReturnsValidationException()
    {
        var details = new CaptureV2ErrorDetails(
            CaptureV2ResultCode.ValidationFailed,
            ErrorCode: (int)CaptureV2ResultCode.ValidationFailed,
            NativeStatus: 0,
            Stage: 0,
            Component: "CaptureInteropV2Recorder",
            Operation: "Start",
            Message: "Validation failed.");

        CaptureNativeException exception = CaptureV2ResultTranslator.CreateException(details);

        exception.Should().BeOfType<CaptureValidationException>();
        exception.ResultCode.Should().Be(CaptureV2ResultCode.ValidationFailed);
        exception.Operation.Should().Be("Start");
    }

    [TestMethod]
    public void ErrorReader_HidesBufferTooSmallAndReturnsMessage()
    {
        CreateRecorder(out var handle);
        using (handle)
        {
            int result = CaptureV2NativeMethods.CtCaptureV2_Pause(handle);
            result.Should().Be((int)CaptureV2ResultCode.InvalidState);

            CaptureV2ErrorDetails details = CaptureV2ErrorReader.GetLastError(
                handle,
                CaptureV2ResultCode.InvalidState);

            details.ResultCode.Should().Be(CaptureV2ResultCode.InvalidState);
            details.Message.Should().Contain("paused");
        }
    }

    [TestMethod]
    public void ExceptionDetails_RemainReadableAfterHandleDisposal()
    {
        CreateRecorder(out var handle);
        CaptureNativeException exception;
        using (handle)
        {
            int result = CaptureV2NativeMethods.CtCaptureV2_Pause(handle);
            exception = CaptureV2ResultTranslator.CreateException(
                CaptureV2ErrorReader.GetLastError(handle, (CaptureV2ResultCode)result));
        }

        exception.Message.Should().Contain("paused");
        exception.Component.Should().Be("CaptureInteropV2Recorder");
        exception.Operation.Should().Be("Pause");
    }

    [TestMethod]
    public void ThrowIfFailed_WithSuccess_DoesNotThrow()
    {
        CreateRecorder(out var handle);
        using (handle)
        {
            Action act = () => CaptureV2ResultTranslator.ThrowIfFailed(handle, (int)CaptureV2ResultCode.Success);

            act.Should().NotThrow();
        }
    }

    private static void CreateRecorder(out CaptureRecorderSafeHandle handle)
    {
        int result = CaptureV2NativeMethods.CtCaptureV2_CreateRecorder(out handle);
        result.Should().Be((int)CaptureV2ResultCode.Success);
    }
}
