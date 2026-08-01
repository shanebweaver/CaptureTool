using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures.ClearRecentCaptures;

public interface IClearRecentCapturesUseCase :
    IUseCase<ClearRecentCapturesRequest, ClearRecentCapturesResponse>,
    IConditional<ClearRecentCapturesRequest>;
