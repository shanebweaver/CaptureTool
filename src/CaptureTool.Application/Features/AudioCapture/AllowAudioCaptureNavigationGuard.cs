using CaptureTool.Application.Abstractions.Features.AudioCapture;

namespace CaptureTool.Application.Features.AudioCapture;

internal sealed class AllowAudioCaptureNavigationGuard : IAudioCaptureNavigationGuard
{
    public Task<bool> CanNavigateAwayFromActiveCaptureAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
