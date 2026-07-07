using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Capture.Video.StopVideoCapture;

public interface IStopVideoCaptureUseCase : IUseCase<StopVideoCaptureRequest, StopVideoCaptureResponse>, IConditional<StopVideoCaptureRequest>
{
}