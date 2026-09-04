using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Audio;

internal sealed class AudioCapturePostProcessor
{
    private readonly IClipboardService _clipboardService;
    private readonly CaptureFileAllocator _fileAllocator;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ILogService _logService;
    private readonly AudioCaptureFileNameGenerator _fileNameGenerator;
    private readonly ICaptureAssetLifecycleService _captureAssetLifecycleService;
    private readonly ITelemetryService? _telemetryService;

    public AudioCapturePostProcessor(
        IClipboardService clipboardService,
        CaptureFileAllocator fileAllocator,
        ISettingsService settingsService,
        IStorageService storageService,
        ITaskEnvironment taskEnvironment,
        ILogService logService,
        AudioCaptureFileNameGenerator fileNameGenerator,
        ICaptureAssetLifecycleService captureAssetLifecycleService,
        ITelemetryService? telemetryService = null)
    {
        _clipboardService = clipboardService;
        _fileAllocator = fileAllocator;
        _settingsService = settingsService;
        _storageService = storageService;
        _taskEnvironment = taskEnvironment;
        _logService = logService;
        _fileNameGenerator = fileNameGenerator;
        _captureAssetLifecycleService = captureAssetLifecycleService;
        _telemetryService = telemetryService;
    }

    public void Process(AudioFile audioFile)
    {
        CaptureId? captureId = TryFinalizeCapture(audioFile.FilePath);
        AutoSaveAudio(audioFile, captureId);
        AutoCopyAudio(audioFile);
    }

    private CaptureId? TryFinalizeCapture(string retainedSourcePath)
    {
        try
        {
            return _captureAssetLifecycleService.TryFinalize(
                retainedSourcePath,
                CaptureFileType.Audio);
        }
        catch (Exception exception)
        {
            _logService.LogException(
                exception,
                "Failed to record the finalized audio Capture Asset.");
            return null;
        }
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
                TrackOutput("auto_copy", TelemetryOutcomes.Succeeded);
            }
            catch (Exception e)
            {
                _logService.LogException(e, "Activity error: AutoCopyAudio");
                TrackOutput("auto_copy", TelemetryOutcomes.Failed);
            }
        });
    }

    private void AutoSaveAudio(AudioFile audioFile, CaptureId? captureId)
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

            string newFilePath = _fileAllocator.CopyToUniqueFile(
                audioFile.FilePath,
                audioFolder,
                _fileNameGenerator.GetNewCaptureFileName);
            _captureAssetLifecycleService.TrySetPreferredOpenPath(
                captureId,
                audioFile.FilePath,
                newFilePath);
            TrackOutput("auto_save", TelemetryOutcomes.Succeeded);
        }
        catch (Exception e)
        {
            _logService.LogException(e, "Activity error: AutoSaveAudio");
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
                [TelemetryProperties.MediaType] = "audio",
                [TelemetryProperties.Outcome] = outcome,
                [TelemetryProperties.Source] = "capture_post_processor"
            });
    }
}
