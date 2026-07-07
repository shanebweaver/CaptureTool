using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;

public interface IOpenRecentCaptureUseCase : IUseCase<OpenRecentCaptureRequest, OpenRecentCaptureResponse>, IConditional<OpenRecentCaptureRequest>
{
}