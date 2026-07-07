namespace CaptureTool.Application.Abstractions.Capture.Audio;

public interface IAudioCaptureNavigationGuard
{
    Task<bool> CanNavigateAwayFromActiveCaptureAsync(CancellationToken cancellationToken = default);
}
