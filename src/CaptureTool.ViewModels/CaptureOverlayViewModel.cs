using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Themes;
using System;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : LoadableViewModelBase
{
    private readonly IAppController _appController;
    private MonitorCaptureResult? _monitorCaptureResult;
    private Rectangle? _captureArea;

    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        private set => Set(ref _isRecording, value);
    }

    private TimeSpan _captureTime;
    public TimeSpan CaptureTime
    {
        get => _captureTime;
        private set => Set(ref _captureTime, value);
    }

    private AppTheme _currentAppTheme;
    public AppTheme CurrentAppTheme
    {
        get => _currentAppTheme;
        private set => Set(ref _currentAppTheme, value);
    }

    private AppTheme _defaultAppTheme;
    public AppTheme DefaultAppTheme
    {
        get => _defaultAppTheme;
        private set => Set(ref _defaultAppTheme, value);
    }

    private bool _isDesktopAudioEnabled;
    public bool IsDesktopAudioEnabled
    {
        get => _isDesktopAudioEnabled;
        set => Set(ref _isDesktopAudioEnabled, value);
    }

    public RelayCommand CloseOverlayCommand => new(CloseOverlay);
    public RelayCommand GoBackCommand => new(GoBack);
    public RelayCommand StartVideoCaptureCommand => new(StartVideoCapture);
    public RelayCommand StopVideoCaptureCommand => new(StopVideoCapture);
    public RelayCommand ToggleDesktopAudioCommand => new(ToggleDesktopAudio);
    

    public CaptureOverlayViewModel(
        IThemeService themeService,
        IAppController appController) 
    {
        _appController = appController;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;
    }

    public override void Load(object? parameter)
    {
        if (parameter is (MonitorCaptureResult monitor, Rectangle area))
        {
            _monitorCaptureResult = monitor;
            _captureArea = area;
        }

        base.Load(parameter);
    }

    public override void Unload()
    {
        base.Unload();
    }

    private void CloseOverlay()
    {
        if (IsRecording)
        {
            _appController.CancelVideoCapture();
        }

        _appController.CloseCaptureOverlay();
        _appController.ShowMainWindow(true);
    }

    private void GoBack()
    {
        if (IsRecording)
        {
            _appController.CancelVideoCapture();
        }

        _appController.CloseCaptureOverlay();
        _appController.ShowSelectionOverlay();
    }

    private void StartVideoCapture()
    {
        if (!IsRecording && _monitorCaptureResult != null && _captureArea != null)
        {
           IsRecording = true;
            _appController.StartVideoCapture(_monitorCaptureResult.Value, _captureArea.Value);
        }
    }

    private void StopVideoCapture()
    {
        if (IsRecording)
        {
            IsRecording = false;
            _appController.StopVideoCapture();
        }
    }

    private void ToggleDesktopAudio()
    {
        IsDesktopAudioEnabled = !IsDesktopAudioEnabled;
    }
}
