using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.ClearTempFiles;

internal sealed class ClearTempFilesUseCase : IClearTempFilesUseCase
{
    private const string ActivityId = "ClearTempFiles";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IScratchArtifactStore _scratchArtifactStore;
    private readonly IRecentCapturesChangeNotifier _recentCapturesChangeNotifier;

    public ClearTempFilesUseCase(
        IScratchArtifactStore scratchArtifactStore,
        IUseCaseExecutor useCaseExecutor,
        IRecentCapturesChangeNotifier recentCapturesChangeNotifier)
    {
        _useCaseExecutor = useCaseExecutor;
        _scratchArtifactStore = scratchArtifactStore;
        _recentCapturesChangeNotifier = recentCapturesChangeNotifier;
    }

    public bool CanExecute(ClearTempFilesRequest request) => true;

    public Task<UseCaseResponse<ClearTempFilesResponse>> ExecuteAsync(ClearTempFilesRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                ScratchArtifactCleanupResult cleanup =
                    _scratchArtifactStore.ClearUnleasedArtifacts();
                _recentCapturesChangeNotifier.NotifyRecentCapturesChanged();
                return new ClearTempFilesResponse(
                    cleanup.DeletedItemCount,
                    cleanup.DeletedByteCount,
                    cleanup.ActiveItemCount,
                    cleanup.ActiveByteCount,
                    cleanup.FailedItemCount);
            },
            cancellationToken: cancellationToken);
    }
}
