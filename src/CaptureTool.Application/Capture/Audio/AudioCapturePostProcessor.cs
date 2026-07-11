using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Capture.Audio;

internal sealed class AudioCapturePostProcessor
{
    private readonly IClipboardService _clipboardService;
    private readonly IFileSystem _fileSystem;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ITelemetryService _telemetryService;
    private readonly AudioCaptureFileNameGenerator _fileNameGenerator;

    public AudioCapturePostProcessor(
        IClipboardService clipboardService,
        IFileSystem fileSystem,
        ISettingsService settingsService,
        IStorageService storageService,
        ITaskEnvironment taskEnvironment,
        ITelemetryService telemetryService,
        AudioCaptureFileNameGenerator fileNameGenerator)
    {
        _clipboardService = clipboardService;
        _fileSystem = fileSystem;
        _settingsService = settingsService;
        _storageService = storageService;
        _taskEnvironment = taskEnvironment;
        _telemetryService = telemetryService;
        _fileNameGenerator = fileNameGenerator;
    }

    public void Process(AudioFile audioFile)
    {
        AutoSaveAudio(audioFile);
        AutoCopyAudio(audioFile);
    }

    private void AutoCopyAudio(AudioFile audioFile)
    {
        _taskEnvironment.TryExecute(async () =>
        {
            try
            {
                bool autoCopy = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoCopy);
                if (!autoCopy)
                {
                    return;
                }

                ClipboardFile clipboardFile = new(audioFile.FilePath);
                await _clipboardService.CopyFileAsync(clipboardFile);
            }
            catch (Exception e)
            {
                TrackPostProcessingException(e, "AutoCopyAudio", "auto_copy_failed");
                _telemetryService.ActivityError("AutoCopyAudio", e);
            }
        });
    }

    private void AutoSaveAudio(AudioFile audioFile)
    {
        try
        {
            bool autoSave = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoSave);
            if (!autoSave)
            {
                return;
            }

            string audioFolder = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder);
            if (string.IsNullOrWhiteSpace(audioFolder))
            {
                audioFolder = _storageService.GetSystemDefaultMusicFolderPath();
            }

            string newFilePath = Path.Combine(audioFolder, _fileNameGenerator.GetNewCaptureFileName());

            _fileSystem.CopyFile(audioFile.FilePath, newFilePath, true);
            TrackAutoSaveCompleted();
        }
        catch (Exception e)
        {
            TrackPostProcessingException(e, "AutoSaveAudio", "auto_save_failed");
            _telemetryService.ActivityError("AutoSaveAudio", e);
        }
    }

    private void TrackAutoSaveCompleted()
    {
        _telemetryService.TrackEvent(
            TelemetryEvents.FileSaved,
            new Dictionary<string, object?>
            {
                [TelemetryAttributes.CommandId] = "capture.auto_save",
                [TelemetryAttributes.MediaType] = "audio",
                [TelemetryAttributes.Surface] = "capture_post_processor"
            });
    }

    private void TrackPostProcessingException(Exception exception, string activityId, string reasonCode)
    {
        _telemetryService.TrackException(
            exception,
            new TelemetryExceptionContext(
                Component: "CapturePostProcessor",
                ActivityId: activityId,
                ReasonCode: reasonCode,
                Attributes: new Dictionary<string, object?>
                {
                    [TelemetryAttributes.MediaType] = "audio",
                    [TelemetryAttributes.Surface] = "capture_post_processor"
                }));
    }
}
