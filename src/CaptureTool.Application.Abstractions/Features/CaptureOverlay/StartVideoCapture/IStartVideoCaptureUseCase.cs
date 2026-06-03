using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;

public interface IStartVideoCaptureUseCase : IUseCase<StartVideoCaptureRequest, StartVideoCaptureResponse>, IConditional<StartVideoCaptureRequest>
{
}