using CaptureTool.Application.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Infrastructure.Windows.Storage;

public sealed class WindowsFolderLauncher : IFolderLauncher
{
    public bool TryOpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/open,\"{folderPath}\"")
            {
                UseShellExecute = true,
            });

            return true;
        }
        catch
        {
            return false;
        }
    }
}
