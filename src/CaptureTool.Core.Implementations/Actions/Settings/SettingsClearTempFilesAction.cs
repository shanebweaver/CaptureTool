using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsClearTempFilesAction : ActionCommand<string>, ISettingsClearTempFilesAction
{
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
            catch
            {
                // Ignore errors
            }
        }
    }
}
