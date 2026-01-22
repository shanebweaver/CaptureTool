using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.Settings;
using CaptureTool.Infrastructure.Interfaces.Logging;

namespace CaptureTool.Application.Implementations.Actions.Settings;

public sealed partial class SettingsClearTempFilesAction : ActionCommand<string>, ISettingsClearTempFilesAction
{
    private readonly ILogService _logService;

    public SettingsClearTempFilesAction(ILogService logService)
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
