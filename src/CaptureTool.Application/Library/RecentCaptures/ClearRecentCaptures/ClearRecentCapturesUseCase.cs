using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Library.RecentCaptures.ClearRecentCaptures;

internal sealed class ClearRecentCapturesUseCase : IClearRecentCapturesUseCase
{
    private const string ActivityId = "ClearRecentCaptures";

    private readonly IRecentCaptureCatalog _recentCaptureCatalog;
    private readonly ICaptureAssetCatalog _captureAssetCatalog;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public ClearRecentCapturesUseCase(
        IRecentCaptureCatalog recentCaptureCatalog,
        ICaptureAssetCatalog captureAssetCatalog,
        IUseCaseExecutor useCaseExecutor)
    {
        _recentCaptureCatalog = recentCaptureCatalog;
        _captureAssetCatalog = captureAssetCatalog;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(ClearRecentCapturesRequest request) => true;

    public Task<UseCaseResponse<ClearRecentCapturesResponse>> ExecuteAsync(
        ClearRecentCapturesRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _recentCaptureCatalog.Clear(_captureAssetCatalog.GetLatestChangeSequence());
                return new ClearRecentCapturesResponse();
            },
            cancellationToken: cancellationToken);
    }
}
