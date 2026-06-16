using CaptureTool.Application.Abstractions.Features.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.ClearTempFiles;

public sealed class ClearTempFilesUseCase : IClearTempFilesUseCase
{
    private const string ActivityId = "ClearTempFiles";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ILogService _logService;
    private readonly IStorageService _storageService;

    public ClearTempFilesUseCase(ILogService logService, IStorageService storageService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _logService = logService;
        _storageService = storageService;
    }

    public bool CanExecute(ClearTempFilesRequest request) => true;

    public Task<UseCaseResponse<ClearTempFilesResponse>> ExecuteAsync(ClearTempFilesRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                string tempFolderPath = _storageService.GetApplicationTemporaryFolderPath();
                foreach (var entry in Directory.EnumerateFileSystemEntries(tempFolderPath))
                {
                    try
                    {
                        if (Directory.Exists(entry))
                        {
                            Directory.Delete(entry, true);
                        }
                        else
                        {
                            File.Delete(entry);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogException(ex, $"Failed to delete temporary file or folder: {entry}");
                    }
                }

                return new ClearTempFilesResponse();
            },
            cancellationToken: cancellationToken);
    }
}
