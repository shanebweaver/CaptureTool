using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Library.RecentCaptures.RemoveRecentCapture;

internal sealed class RemoveRecentCaptureUseCase : IRemoveRecentCaptureUseCase
{
    private const string ActivityId = "RemoveRecentCapture";

    private readonly IRecentCaptureCatalog _recentCaptureCatalog;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ICaptureAssetRemovalService? _captureRemoval;

    public RemoveRecentCaptureUseCase(
        IRecentCaptureCatalog recentCaptureCatalog,
        IUseCaseExecutor useCaseExecutor,
        ICaptureAssetRemovalService? captureRemoval = null)
    {
        _recentCaptureCatalog = recentCaptureCatalog;
        _useCaseExecutor = useCaseExecutor;
        _captureRemoval = captureRemoval;
    }

    public bool CanExecute(RemoveRecentCaptureRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.FilePath);
    }

    public Task<UseCaseResponse<RemoveRecentCaptureResponse>> ExecuteAsync(
        RemoveRecentCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: token => RemoveAsync(request.FilePath, token),
            cancellationToken: cancellationToken);
    }

    private async Task<RemoveRecentCaptureResponse> RemoveAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        RecentCaptureCatalogEntry? entry = (_recentCaptureCatalog.GetEntries() ?? [])
            .FirstOrDefault(candidate => string.Equals(
                candidate.FilePath,
                filePath,
                StringComparison.OrdinalIgnoreCase));
        if (entry?.CaptureId is not CaptureTool.Domain.CaptureId captureId ||
            _captureRemoval == null)
        {
            return new(_recentCaptureCatalog.Remove(filePath));
        }

        CaptureAssetRemovalResult result = await _captureRemoval.RemoveAsync(
            new CaptureAssetRemovalRequest(captureId, CaptureAssetRemovalKind.ForgetHistory),
            cancellationToken).ConfigureAwait(false);
        return new(result.Status is CaptureAssetRemovalStatus.Succeeded or
            CaptureAssetRemovalStatus.AlreadyRemoved);
    }
}
