namespace CaptureTool.Application.Abstractions.Capture.Audio;

public interface IAudioCaptureNavigationConfirmationService
{
    Task<bool> ConfirmStopActiveRecordingAsync(CancellationToken cancellationToken = default);
}
