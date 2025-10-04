using CaptureTool.Common.Commands;
using CaptureTool.Common.Storage;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Clipboard;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Telemetry;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class VideoEditPageViewModel : AsyncLoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = $"{nameof(VideoEditPageViewModel)}_Load";
        public static readonly string Unload = $"{nameof(VideoEditPageViewModel)}_Unload";
        public static readonly string Save = $"{nameof(VideoEditPageViewModel)}_Save";
        public static readonly string Copy = $"{nameof(VideoEditPageViewModel)}_Copy";
    }

    public RelayCommand SaveCommand => new(Save);
    public RelayCommand CopyCommand => new(Copy);

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
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is VideoFile video)
        {
            VideoPath = video.Path;
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        _videoPath = null;
        base.Unload();
    }

    private async void Save()
    {
        string activityId = ActivityIds.Save;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            nint hwnd = _appController.GetMainWindowHandle();
            VideoFile? file = await _filePickerService.SaveVideoFileAsync(hwnd);
            if (file is not null && !string.IsNullOrEmpty(_videoPath))
            {
                File.Copy(_videoPath, file.Path, true);
                _telemetryService.ActivityCompleted(activityId);
            }
            else
            {
                _telemetryService.ActivityCompleted(activityId, "User canceled");
            }
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private async void Copy()
    {
        string activityId = ActivityIds.Copy;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            if (string.IsNullOrEmpty(_videoPath))
            {
                throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
            }

            ClipboardVideoWrapper clipboardVideo = new(_videoPath);
            await _clipboardService.CopyVideoAsync(clipboardVideo);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
