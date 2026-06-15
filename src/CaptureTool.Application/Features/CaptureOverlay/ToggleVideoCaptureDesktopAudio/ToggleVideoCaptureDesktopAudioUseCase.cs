using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;

namespace CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;

public sealed class ToggleVideoCaptureDesktopAudioUseCase : IToggleVideoCaptureDesktopAudioUseCase
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public ToggleVideoCaptureDesktopAudioUseCase(IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    public Task<ToggleVideoCaptureDesktopAudioResponse> ExecuteAsync(ToggleVideoCaptureDesktopAudioRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            bool newValue = !_videoCaptureHandler.IsDesktopAudioEnabled;
            _videoCaptureHandler.SetIsDesktopAudioEnabled(newValue);
            _videoCaptureHandler.ToggleDesktopAudioCapture(newValue);
            return Task.FromResult(new ToggleVideoCaptureDesktopAudioResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new ToggleVideoCaptureDesktopAudioResponse(false));
        }
    }
}
