using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;

public sealed class ToggleVideoCaptureDesktopAudioUseCase : IUseCase<ToggleVideoCaptureDesktopAudioRequest, ToggleVideoCaptureDesktopAudioResponse>
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public ToggleVideoCaptureDesktopAudioUseCase(IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    public Task<ToggleVideoCaptureDesktopAudioResponse> ExecuteAsync(ToggleVideoCaptureDesktopAudioRequest request, CancellationToken cancellationToken = default)
    {
        bool newValue = !_videoCaptureHandler.IsDesktopAudioEnabled;
        _videoCaptureHandler.SetIsDesktopAudioEnabled(newValue);
        _videoCaptureHandler.ToggleDesktopAudioCapture(newValue);
        return Task.FromResult(new ToggleVideoCaptureDesktopAudioResponse());
    }
}