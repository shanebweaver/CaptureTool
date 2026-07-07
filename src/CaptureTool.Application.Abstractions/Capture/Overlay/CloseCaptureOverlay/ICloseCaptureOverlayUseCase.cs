using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Capture.Overlay.CloseCaptureOverlay;

public interface ICloseCaptureOverlayUseCase : IUseCase<CloseCaptureOverlayRequest, CloseCaptureOverlayResponse>, IConditional<CloseCaptureOverlayRequest>
{
}