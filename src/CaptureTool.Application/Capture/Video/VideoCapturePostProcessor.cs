using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Video;

internal sealed class VideoCapturePostProcessor
{
    private readonly IClipboardService _clipboardService;
    private readonly IFileSystem _fileSystem;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ILogService _logService;
    private readonly VideoCaptureFileNameGenerator _fileNameGenerator;
    private readonly IRecentCaptureCatalog _recentCaptureCatalog;
    private readonly ITelemetryService? _telemetryService;

    public VideoCapturePostProcessor(
        IClipboardService clipboardService,
        IFileSystem fileSystem,
        ISettingsService settingsService,
        IStorageService storageService,
        ITaskEnvironment taskEnvironment,
        ILogService logService,
        VideoCaptureFileNameGenerator fileNameGenerator,
        IRecentCaptureCatalog recentCaptureCatalog,
        ITelemetryService? telemetryService = null)
    {
        _clipboardService = clipboardService;
        _fileSystem = fileSystem;
        _settingsService = settingsService;
        _storageService = storageService;
        _taskEnvironment = taskEnvironment;
        _logService = logService;
        _fileNameGenerator = fileNameGenerator;
        _recentCaptureCatalog = recentCaptureCatalog;
        _telemetryService = telemetryService;
    }

    public void Process(VideoFile videoFile)
    {
        _recentCaptureCatalog.RecordCaptured(videoFile.FilePath, CaptureFileType.Video);
        AutoSaveVideo(videoFile);
        AutoCopyVideo(videoFile);
    }

    private void AutoCopyVideo(VideoFile videoFile)
    {
        _taskEnvironment.TryExecute(async () =>
        {
            try
            {
                bool autoCopy = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
                if (!autoCopy)
                {
                    return;
                }

                ClipboardFile clipboardFile = new(videoFile.FilePath);
                await _clipboardService.CopyFileAsync(clipboardFile);
                TrackOutput("auto_copy", TelemetryOutcomes.Succeeded);
            }
            catch (Exception e)
            {
                _logService.LogException(e, "Activity error: AutoCopyVideo");
                TrackOutput("auto_copy", TelemetryOutcomes.Failed);
            }
        });
    }

    private void AutoSaveVideo(VideoFile videoFile)
    {
        try
        {
            bool autoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);
            if (!autoSave)
            {
                return;
            }

            string videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
            if (string.IsNullOrWhiteSpace(videosFolder))
            {
                videosFolder = _storageService.GetSystemDefaultVideosFolderPath();
            }

            string newFilePath = Path.Combine(videosFolder, _fileNameGenerator.GetNewCaptureFileName());

            _fileSystem.CopyFile(videoFile.FilePath, newFilePath, true);
            _recentCaptureCatalog.ReplacePath(videoFile.FilePath, newFilePath);
            TrackOutput("auto_save", TelemetryOutcomes.Succeeded);
        }
        catch (Exception e)
        {
            _logService.LogException(e, "Activity error: AutoSaveVideo");
            TrackOutput("auto_save", TelemetryOutcomes.Failed);
        }
    }

    private void TrackOutput(string operation, string outcome)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.OutputCompleted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Operation] = operation,
                [TelemetryProperties.MediaType] = "video",
                [TelemetryProperties.Outcome] = outcome,
                [TelemetryProperties.Source] = "capture_post_processor"
            });
    }
}
