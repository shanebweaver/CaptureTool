using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestApplicationLocalCachePathProvider : IApplicationLocalCachePathProvider
{
    private readonly string _localCacheFolderPath;

    public UiTestApplicationLocalCachePathProvider(UiTestLaunchOptions options)
    {
        string temporaryFolderPath = options.TemporaryFolderPath ?? Path.Combine(
            Path.GetTempPath(),
            "CaptureToolUiTests",
            Guid.NewGuid().ToString("N"));
        _localCacheFolderPath = Path.Combine(
            Path.GetFullPath(temporaryFolderPath),
            "LocalCache");
    }

    public string GetApplicationLocalCacheFolderPath()
    {
        Directory.CreateDirectory(_localCacheFolderPath);
        return _localCacheFolderPath;
    }
}
