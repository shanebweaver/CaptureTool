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
    public class ClipboardFileWrapper : IClipboardFile
    {
        public string FilePath { get; }

        public ClipboardFileWrapper(string filePath)
        {
            FilePath = filePath;
        }
    }

    public readonly struct ActivityIds
    {
        public static readonly string Load = $"LoadVideoEditPage";
        public static readonly string Save = $"Save";
        public static readonly string Copy = $"Copy";
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand CopyCommand { get; }

    private string? _videoPath;
    public string? VideoPath
    {
        get => _videoPath;
        set => Set(ref _videoPath, value);
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

        SaveCommand = new(Save);
        CopyCommand = new(Copy);
    }

    public override void Load(VideoFile video)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Load, () =>
        {
            VideoPath = video.FilePath;

            base.Load(video);
        });
    }

    public override void Dispose()
    {
        _videoPath = null;
        base.Dispose();
    }

    private async void Save()
    {
        await TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Save, async () =>
        {
            if (string.IsNullOrEmpty(_videoPath))
            {
                throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
            }

            nint hwnd = _appController.GetMainWindowHandle();
            IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FileType.Video, UserFolder.Videos)
                ?? throw new OperationCanceledException("No file was selected.");
        
            File.Copy(_videoPath, file.FilePath, true);
        });
    }

    private async void Copy()
    {
        await TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Copy, async () =>
        {
            if (string.IsNullOrEmpty(_videoPath))
            {
                throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
            }

            ClipboardFileWrapper clipboardVideo = new(_videoPath);
            await _clipboardService.CopyFileAsync(clipboardVideo);
        });
    }
}
