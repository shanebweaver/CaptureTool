using CaptureTool.Application.Abstractions.Features.AudioCapture;

namespace CaptureTool.Application.Features.AudioCapture;

public sealed class AllowAudioCaptureNavigationGuard : IAudioCaptureNavigationGuard
{
    public Task<bool> CanNavigateAwayFromActiveCaptureAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
