using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.CaptureOverlay.StopVideoCapture;

public interface IStopVideoCaptureUseCase : IUseCase<StopVideoCaptureRequest, StopVideoCaptureResponse>, IConditional<StopVideoCaptureRequest>
{
}