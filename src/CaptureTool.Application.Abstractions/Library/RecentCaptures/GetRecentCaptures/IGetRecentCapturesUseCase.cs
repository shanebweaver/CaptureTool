using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;

public interface IGetRecentCapturesUseCase : IUseCase<GetRecentCapturesRequest, GetRecentCapturesResponse>, IConditional<GetRecentCapturesRequest>
{
}