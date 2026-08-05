namespace CaptureTool.Application.Abstractions.Capture;

public sealed record AudioCaptureFailure(AudioCaptureFailureStage Stage, string Message)
{
    private const int MaximumMessageLength = 1024;

    public static AudioCaptureFailure FromException(AudioCaptureFailureStage stage, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string message = exception.Message ?? string.Empty;
        if (message.Length > MaximumMessageLength)
        {
            message = message[..MaximumMessageLength];
        }

        return new AudioCaptureFailure(stage, message);
    }
}

public enum AudioCaptureFailureStage
{
    RecorderStop,
    PostProcessing,
}
