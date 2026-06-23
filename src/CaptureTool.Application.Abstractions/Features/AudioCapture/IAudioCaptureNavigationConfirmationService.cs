namespace CaptureTool.Application.Abstractions.Features.AudioCapture;

public interface IAudioCaptureNavigationConfirmationService
{
    Task<bool> ConfirmStopActiveRecordingAsync(CancellationToken cancellationToken = default);
}
