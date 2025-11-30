using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Telemetry;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Clipboard;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;

namespace CaptureTool.ViewModels;
public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<VideoFile>
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = $"LoadVideoEditPage";
        public static readonly string Save = $"Save";
        public static readonly string Copy = $"Copy";
    }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CopyCommand { get; }

    public string? VideoPath
    {
        get => field;
        private set => Set(ref field, value);
    }

    private readonly IClipboardService _clipboardService;
    private readonly IFilePickerService _filePickerService;
    private readonly IAppController _appController;
    private readonly ITelemetryService _telemetryService;

    public VideoEditPageViewModel(
        IClipboardService clipboardService,
        IFilePickerService filePickerService,
        IAppController appController,
        ITelemetryService telemetryService)
    {
        _clipboardService = clipboardService;
        _filePickerService = filePickerService;
        _appController = appController;
        _telemetryService = telemetryService;

        SaveCommand = new(SaveAsync);
        CopyCommand = new(CopyAsync);
    }

    public override void Load(VideoFile video)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Load, () =>
        {
            VideoPath = video.FilePath;

            base.Load(video);
        });
    }

    private Task SaveAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Save, async () =>
        {
            if (string.IsNullOrEmpty(VideoPath))
            {
                throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
            }

            nint hwnd = _appController.GetMainWindowHandle();
            IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FileType.Video, UserFolder.Videos)
                ?? throw new OperationCanceledException("No file was selected.");
        
            File.Copy(VideoPath, file.FilePath, true);
        });
    }

    private Task CopyAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Copy, async () =>
        {
            if (string.IsNullOrEmpty(VideoPath))
            {
                throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
            }

            ClipboardFile clipboardVideo = new(VideoPath);
            await _clipboardService.CopyFileAsync(clipboardVideo);
        });
    }
}
