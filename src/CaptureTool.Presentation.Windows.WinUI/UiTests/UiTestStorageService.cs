using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestStorageService : IStorageService
{
    private readonly string _dataFolderPath;
    private readonly string _temporaryFolderPath;

    public UiTestStorageService(UiTestLaunchOptions options)
    {
        string basePath = Path.Combine(Path.GetTempPath(), "CaptureToolUiTests", Guid.NewGuid().ToString("N"));
        _dataFolderPath = options.DataFolderPath ?? Path.Combine(basePath, "Data");
        _temporaryFolderPath = options.TemporaryFolderPath ?? Path.Combine(basePath, "Temp");

        Directory.CreateDirectory(_dataFolderPath);
        Directory.CreateDirectory(_temporaryFolderPath);
    }

    public string GetApplicationDataFolderPath()
    {
        return _dataFolderPath;
    }

    public string GetSystemDefaultScreenshotsFolderPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    public string GetApplicationTemporaryFolderPath()
    {
        return _temporaryFolderPath;
    }

    public string GetTemporaryFileName()
    {
        return $"{Guid.NewGuid():N}.tmp";
    }

    public string GetSystemDefaultVideosFolderPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    }

    public string GetSystemDefaultMusicFolderPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    }
}
