using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Features.VideoCapture;

internal sealed class VideoCapturePostProcessor
{
    private readonly IClipboardService _clipboardService;
    private readonly IFileSystem _fileSystem;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ITelemetryService _telemetryService;
    private readonly VideoCaptureFileNameGenerator _fileNameGenerator;

    public VideoCapturePostProcessor(
        IClipboardService clipboardService,
        IFileSystem fileSystem,
        ISettingsService settingsService,
        IStorageService storageService,
        ITaskEnvironment taskEnvironment,
        ITelemetryService telemetryService,
        VideoCaptureFileNameGenerator fileNameGenerator)
    {
        _clipboardService = clipboardService;
        _fileSystem = fileSystem;
        _settingsService = settingsService;
        _storageService = storageService;
        _taskEnvironment = taskEnvironment;
        _telemetryService = telemetryService;
        _fileNameGenerator = fileNameGenerator;
    }

    public void Process(VideoFile videoFile)
    {
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
            }
            catch (Exception e)
            {
                _telemetryService.ActivityError("AutoCopyVideo", e);
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
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError("AutoSaveVideo", e);
        }
    }
}
