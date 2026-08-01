using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures.RemoveRecentCapture;

public interface IRemoveRecentCaptureUseCase :
    IUseCase<RemoveRecentCaptureRequest, RemoveRecentCaptureResponse>,
    IConditional<RemoveRecentCaptureRequest>;
