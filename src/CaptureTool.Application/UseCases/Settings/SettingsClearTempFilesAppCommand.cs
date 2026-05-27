using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.UseCases.Settings;

internal class SettingsClearTempFilesAppCommand : ISettingsClearTempFilesAppCommand
{
    public SettingsClearTempFilesAppCommand(
        ILogService logService,
        IStorageService storageService)
    {
        _logService = logService;
        _storageService = storageService;
    }

    private readonly ILogService _logService;
    private readonly IStorageService _storageService;

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
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
    }
}
