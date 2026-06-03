using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.RecentCaptures.GetRecentCaptures;

public interface IGetRecentCapturesUseCase : IUseCase<GetRecentCapturesRequest, GetRecentCapturesResponse>, IConditional<GetRecentCapturesRequest>
{
}