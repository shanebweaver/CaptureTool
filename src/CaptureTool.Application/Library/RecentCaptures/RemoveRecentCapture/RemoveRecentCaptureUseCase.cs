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

    public RemoveRecentCaptureUseCase(
        IRecentCaptureCatalog recentCaptureCatalog,
        IUseCaseExecutor useCaseExecutor)
    {
        _recentCaptureCatalog = recentCaptureCatalog;
        _useCaseExecutor = useCaseExecutor;
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
            useCase: () => new RemoveRecentCaptureResponse(
                _recentCaptureCatalog.Remove(request.FilePath)),
            cancellationToken: cancellationToken);
    }
}
