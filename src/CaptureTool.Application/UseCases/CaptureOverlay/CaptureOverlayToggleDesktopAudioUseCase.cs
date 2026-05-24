using CaptureTool.Application.Abstractions.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.UseCases.CaptureOverlay;

public sealed partial class CaptureOverlayToggleDesktopAudioUseCase : UseCase, ICaptureOverlayToggleDesktopAudioUseCase
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayToggleDesktopAudioUseCase(
        IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    public override void Execute()
    {
        bool newValue = !_videoCaptureHandler.IsDesktopAudioEnabled;
        _videoCaptureHandler.SetIsDesktopAudioEnabled(newValue);
        _videoCaptureHandler.ToggleDesktopAudioCapture(newValue);
    }
}
