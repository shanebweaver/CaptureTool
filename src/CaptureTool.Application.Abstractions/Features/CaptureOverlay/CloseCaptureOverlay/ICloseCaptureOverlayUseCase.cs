using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.CaptureOverlay.CloseCaptureOverlay;

public interface ICloseCaptureOverlayUseCase : IUseCase<CloseCaptureOverlayRequest, CloseCaptureOverlayResponse>, IConditional<CloseCaptureOverlayRequest>
{
}