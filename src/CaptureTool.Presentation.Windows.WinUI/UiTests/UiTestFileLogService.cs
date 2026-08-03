using CaptureTool.Infrastructure.Logging;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestFileLogService : LogServiceBase
{
    private readonly Lock _lock = new();
    private readonly string _logFilePath;

    public UiTestFileLogService(UiTestLaunchOptions options)
    {
        string logDirectory = options.TemporaryFolderPath ?? Path.Combine(
            Path.GetTempPath(),
            "CaptureToolUiTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, "capturetool.log");
        Enable();
    }

    protected override void AddLogEntry(string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (_lock)
        {
            base.AddLogEntry(message);
            File.AppendAllText(
                _logFilePath,
                $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
    }
}
