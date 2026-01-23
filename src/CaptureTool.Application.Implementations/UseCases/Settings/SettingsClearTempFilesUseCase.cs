using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.UseCases;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Interfaces.Logging;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsClearTempFilesUseCase : UseCase<string>, ISettingsClearTempFilesUseCase
{
    private readonly ILogService _logService;

    public SettingsClearTempFilesUseCase(ILogService logService)
    {
        _logService = logService;
    }

    public override void Execute(string tempFolderPath)
    {
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
