namespace CaptureTool.Application.Abstractions.Capture;

public sealed class MicrophoneAccessDeniedException : UnauthorizedAccessException
{
    public MicrophoneAccessDeniedException()
        : base("Windows has denied microphone access.")
    {
    }
}
