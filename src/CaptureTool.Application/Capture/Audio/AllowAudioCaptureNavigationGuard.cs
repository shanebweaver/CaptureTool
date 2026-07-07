using CaptureTool.Application.Abstractions.Capture.Audio;

namespace CaptureTool.Application.Capture.Audio;

internal sealed class AllowAudioCaptureNavigationGuard : IAudioCaptureNavigationGuard
{
    public Task<bool> CanNavigateAwayFromActiveCaptureAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
