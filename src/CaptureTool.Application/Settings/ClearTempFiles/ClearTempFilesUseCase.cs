using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.ClearTempFiles;

internal sealed class ClearTempFilesUseCase : IClearTempFilesUseCase
{
    private const string ActivityId = "ClearTempFiles";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ILogService _logService;
    private readonly IStorageService _storageService;
    private readonly IFileSystem _fileSystem;
    private readonly IRecentCapturesChangeNotifier _recentCapturesChangeNotifier;

    public ClearTempFilesUseCase(ILogService logService, IStorageService storageService,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor,
        IRecentCapturesChangeNotifier recentCapturesChangeNotifier)
    {
        _useCaseExecutor = useCaseExecutor;
        _logService = logService;
        _storageService = storageService;
        _fileSystem = fileSystem;
        _recentCapturesChangeNotifier = recentCapturesChangeNotifier;
    }

    public bool CanExecute(ClearTempFilesRequest request) => true;

    public Task<UseCaseResponse<ClearTempFilesResponse>> ExecuteAsync(ClearTempFilesRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                string tempFolderPath = _storageService.GetApplicationTemporaryFolderPath();
                foreach (var entry in _fileSystem.EnumerateFileSystemEntries(tempFolderPath))
                {
                    try
                    {
                        if (_fileSystem.DirectoryExists(entry))
                        {
                            _fileSystem.DeleteDirectory(entry, true);
                        }
                        else
                        {
                            _fileSystem.DeleteFile(entry);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogException(ex, $"Failed to delete temporary file or folder: {entry}");
                    }
                }

                _recentCapturesChangeNotifier.NotifyRecentCapturesChanged();
                return new ClearTempFilesResponse();
            },
            cancellationToken: cancellationToken);
    }
}
