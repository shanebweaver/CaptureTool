using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Capture.Overlay.GoBackFromCaptureOverlay;

public interface IGoBackFromCaptureOverlayUseCase : IUseCase<GoBackFromCaptureOverlayRequest, GoBackFromCaptureOverlayResponse>, IConditional<GoBackFromCaptureOverlayRequest>
{
}