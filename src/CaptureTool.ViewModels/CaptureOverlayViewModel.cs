using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Themes;
using System;
using System.Drawing;
using System.Timers;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : LoadableViewModelBase
{
    public readonly partial struct Options(MonitorCaptureResult monitor, Rectangle area)
    {
        public MonitorCaptureResult Monitor { get; } = monitor;
        public Rectangle Area { get; } = area;
    }

    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;
    private MonitorCaptureResult? _monitorCaptureResult;
    private Rectangle? _captureArea;

    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
    private Timer? _timer;
    private DateTime _captureStartTime;

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
        INavigationService navigationService,
        IThemeService themeService,
        IAppController appController) 
    {
        _navigationService = navigationService;
        _appController = appController;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;
    }

    public override void Load(object? parameter)
    {
        if (parameter is Options options)
        {
            _monitorCaptureResult = options.Monitor;
            _captureArea = options.Area;
        }

        base.Load(parameter);
    }

    public override void Dispose()
    {
        StopTimer();
        base.Dispose();
    }

    private void CloseOverlay()
    {
        if (IsRecording)
        {
            _appController.CancelVideoCapture();
        }

        if (_navigationService.CanGoBack)
        {
            bool success = _navigationService.TryGoBackWhile(r => r.Route == CaptureToolNavigationRoutes.ImageCapture || r.Route == CaptureToolNavigationRoutes.VideoCapture);
            if (!success)
            {
                _appController.GoHome();
            }
        }
        else
        {
            _appController.Shutdown();
        }
    }

    private void GoBack()
    {
        if (IsRecording)
        {
            _appController.CancelVideoCapture();
        }

        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
        else
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.Home, clearHistory: true);
        }
    }

    private void StartVideoCapture()
    {
        if (!IsRecording && _monitorCaptureResult != null && _captureArea != null)
        {
            IsRecording = true;
            CaptureTime = TimeSpan.Zero;
            _captureStartTime = DateTime.UtcNow;
            StartTimer();
            _appController.StartVideoCapture(new(_monitorCaptureResult.Value, _captureArea.Value));
        }
    }

    private void StopVideoCapture()
    {
        if (IsRecording)
        {
            IsRecording = false;
            StopTimer();
            _appController.StopVideoCapture();
        }
    }

    private void ToggleDesktopAudio()
    {
        IsDesktopAudioEnabled = !IsDesktopAudioEnabled;
    }

    private void StartTimer()
    {
        if (_timer == null)
        {
            _timer = new Timer(TimerInterval.TotalMilliseconds);
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
        }
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        CaptureTime = DateTime.UtcNow - _captureStartTime;
    }
}
