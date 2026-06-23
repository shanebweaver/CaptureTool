namespace CaptureTool.Application.Abstractions.Features.AudioCapture;

public interface IAudioCaptureNavigationGuard
{
    Task<bool> CanNavigateAwayFromActiveCaptureAsync(CancellationToken cancellationToken = default);
}
