using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.RecentCaptures.OpenRecentCapture;

public interface IOpenRecentCaptureUseCase : IUseCase<OpenRecentCaptureRequest, OpenRecentCaptureResponse>, IConditional<OpenRecentCaptureRequest>
{
}