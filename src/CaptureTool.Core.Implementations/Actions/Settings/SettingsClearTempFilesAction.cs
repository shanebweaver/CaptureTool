using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Services.Interfaces.Logging;

namespace CaptureTool.Core.Implementations.Actions.Settings;

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
