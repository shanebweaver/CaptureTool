using CaptureTool.Application.Abstractions.Features.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Application.Features.SettingsPage.ClearTempFiles;

public sealed class ClearTempFilesUseCase : IClearTempFilesUseCase
{
    private readonly ILogService _logService;
    private readonly IStorageService _storageService;

    public ClearTempFilesUseCase(ILogService logService, IStorageService storageService)
    {
        _logService = logService;
        _storageService = storageService;
    }

    public bool CanExecute(ClearTempFilesRequest request) => true;

    public Task<ClearTempFilesResponse> ExecuteAsync(ClearTempFilesRequest request, CancellationToken cancellationToken = default)
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

        return Task.FromResult(new ClearTempFilesResponse());
    }
}