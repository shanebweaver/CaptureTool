using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.CaptureOverlay.CancelVideoCapture;

public interface ICancelVideoCaptureUseCase : IUseCase<CancelVideoCaptureRequest, CancelVideoCaptureResponse>
{
}
