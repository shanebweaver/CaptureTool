namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal enum CaptureV2ResultCode
{
    Success = 0,
    InvalidArgument = 1,
    InvalidHandle = 2,
    InvalidState = 3,
    UnsupportedVersion = 4,
    UnsupportedOperation = 5,
    ValidationFailed = 6,
    NotFound = 7,
    AlreadyStarted = 8,
    AlreadyStopped = 9,
    BufferTooSmall = 10,
    NativeFailure = 11,
    ExternalApiFailure = 12,
    CallbackRegistrationFailed = 13,
    CallbackInvocationFailed = 14,
}
