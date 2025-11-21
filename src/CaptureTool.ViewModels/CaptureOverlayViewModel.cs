using CaptureTool.Capture;
using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Telemetry;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Themes;
using System.Drawing;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : LoadableViewModelBase<CaptureOverlayViewModelOptions>
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "CaptureOverlayViewModel_Load";
        public static readonly string CloseOverlay = "CaptureOverlayViewModel_CloseOverlay";
        public static readonly string GoBack = "CaptureOverlayViewModel_GoBack";
        public static readonly string StartVideoCapture = "CaptureOverlayViewModel_StartVideoCapture";
        public static readonly string StopVideoCapture = "CaptureOverlayViewModel_StopVideoCapture";
        public static readonly string ToggleDesktopAudio = "CaptureOverlayViewModel_ToggleDesktopAudio";
    }

    private readonly IAppNavigation _appNavigation;
    private readonly IAppController _appController;
    private readonly ITelemetryService _telemetryService;

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
        IAppNavigation appNavigation,
        IThemeService themeService,
        IAppController appController,
        ITelemetryService telemetryService) 
    {
        _appNavigation = appNavigation;
        _appController = appController;
        _telemetryService = telemetryService;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;
    }

    public override void Load(CaptureOverlayViewModelOptions options)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Load, () =>
        {
            _monitorCaptureResult = options.Monitor;
            _captureArea = options.Area;

            base.Load(options);
        });
    }

    public override void Dispose()
    {
        StopTimer();
        base.Dispose();
    }

    private void CloseOverlay()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.CloseOverlay, () =>
        {
            if (IsRecording)
            {
                _appController.CancelVideoCapture();
            }

            if (_appNavigation.CanGoBack)
            {
                _appNavigation.GoBackToMainWindow();
            }
            else
            {
                _appController.Shutdown();
            }
        });
    }

    private void GoBack()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.GoBack, () =>
        {
            if (IsRecording)
            {
                _appController.CancelVideoCapture();
            }

            if (!_appNavigation.TryGoBack())
            {
                _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault, true);
            }
        });
    }

    private void StartVideoCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.StartVideoCapture, () =>
        {
            if (!IsRecording && _monitorCaptureResult != null && _captureArea != null)
            {
                IsRecording = true;
                CaptureTime = TimeSpan.Zero;
                _captureStartTime = DateTime.UtcNow;
                StartTimer();
                _appController.StartVideoCapture(new(_monitorCaptureResult.Value, _captureArea.Value));
            }
            else
            {
                throw new InvalidOperationException("Cannot start video capture. Monitor or capture area is not set.");
            }
        });
    }

    private void StopVideoCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.StopVideoCapture, () =>
        {
            if (IsRecording)
            {
                IsRecording = false;
                StopTimer();
                _appController.StopVideoCapture();
            }
            else
            {
                throw new InvalidOperationException("Cannot stop video capture. No recording is in progress.");
            }
        });
    }

    private void ToggleDesktopAudio()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ToggleDesktopAudio, () =>
        {
            IsDesktopAudioEnabled = !IsDesktopAudioEnabled;
        });
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
