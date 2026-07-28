using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures.DeleteRecentCapture;

public interface IDeleteRecentCaptureUseCase :
    IUseCase<DeleteRecentCaptureRequest, DeleteRecentCaptureResponse>,
    IConditional<DeleteRecentCaptureRequest>
{
}
