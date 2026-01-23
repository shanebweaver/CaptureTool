using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.UseCases;
using CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Implementations.UseCases.CaptureOverlay;

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
