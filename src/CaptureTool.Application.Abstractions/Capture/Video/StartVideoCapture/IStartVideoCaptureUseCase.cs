using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;

public interface IStartVideoCaptureUseCase : IUseCase<StartVideoCaptureRequest, StartVideoCaptureResponse>, IConditional<StartVideoCaptureRequest>
{
}