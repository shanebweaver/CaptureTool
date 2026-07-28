using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.DeleteRecentCapture;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Library.RecentCaptures.DeleteRecentCapture;

internal sealed class DeleteRecentCaptureUseCase : IDeleteRecentCaptureUseCase
{
    private const string ActivityId = "DeleteRecentCapture";

    private readonly IFileSystem _fileSystem;
    private readonly IStorageService _storageService;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IRecentCapturesChangeNotifier _recentCapturesChangeNotifier;

    public DeleteRecentCaptureUseCase(
        IFileSystem fileSystem,
        IStorageService storageService,
        IUseCaseExecutor useCaseExecutor,
        IRecentCapturesChangeNotifier recentCapturesChangeNotifier)
    {
        _fileSystem = fileSystem;
        _storageService = storageService;
        _useCaseExecutor = useCaseExecutor;
        _recentCapturesChangeNotifier = recentCapturesChangeNotifier;
    }

    public bool CanExecute(DeleteRecentCaptureRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.FilePath);
    }

    public Task<UseCaseResponse<DeleteRecentCaptureResponse>> ExecuteAsync(
        DeleteRecentCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                string temporaryFolderPath = Path.GetFullPath(
                    _storageService.GetApplicationTemporaryFolderPath());
                string recentCapturePath = Path.GetFullPath(request.FilePath);
                string? containingFolderPath = Path.GetDirectoryName(recentCapturePath);

                if (!string.Equals(
                    Path.TrimEndingDirectorySeparator(temporaryFolderPath),
                    containingFolderPath,
                    StringComparison.OrdinalIgnoreCase)
                    || !_fileSystem.FileExists(recentCapturePath))
                {
                    return new DeleteRecentCaptureResponse(false);
                }

                _fileSystem.DeleteFile(recentCapturePath);
                _recentCapturesChangeNotifier.NotifyRecentCapturesChanged();

                return new DeleteRecentCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
