using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsClearTempFilesAction : ActionCommand<string>, ISettingsClearTempFilesAction
{
    public override void Execute(string tempFolderPath)
    {
        if (string.IsNullOrWhiteSpace(tempFolderPath))
        {
            throw new ArgumentException("Temporary folder path cannot be null or empty.", nameof(tempFolderPath));
        }

        if (!Directory.Exists(tempFolderPath))
        {
            // Nothing to clear if folder doesn't exist
            return;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(tempFolderPath))
        {
            try
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }
            }
            catch
            {
                // Ignore errors - some files might be in use
            }
        }
    }
}
