using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.TaskEnvironment;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Themes;
using CaptureTool.ViewModels.Helpers;
using System.Drawing;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : LoadableViewModelBase<CaptureOverlayViewModelOptions>
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadCaptureOverlay";
        public static readonly string CloseOverlay = "CloseOverlay";
        public static readonly string GoBack = "GoBack";
        public static readonly string StartVideoCapture = "StartVideoCapture";
        public static readonly string StopVideoCapture = "StopVideoCapture";
        public static readonly string ToggleDesktopAudio = "ToggleDesktopAudio";
    }

    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly ITelemetryService _telemetryService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ICaptureOverlayActions _captureOverlayActions;

    private MonitorCaptureResult? _monitorCaptureResult;
    private Rectangle? _captureArea;

    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
    private Timer? _timer;
    private DateTime _captureStartTime;

    public bool IsRecording
    {
        get => field;
        private set => Set(ref field, value);
    }

    public TimeSpan CaptureTime
    {
        get => field;
        private set => Set(ref field, value);
    }

    public AppTheme CurrentAppTheme
    {
        get => field;
        private set => Set(ref field, value);
    }

    public AppTheme DefaultAppTheme
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsDesktopAudioEnabled
    {
        get => field;
        private set => Set(ref field, value);
    }

    public RelayCommand CloseOverlayCommand { get; }
    public RelayCommand GoBackCommand { get; }
    public RelayCommand StartVideoCaptureCommand { get; }
    public RelayCommand StopVideoCaptureCommand { get; }
    public RelayCommand ToggleDesktopAudioCommand { get; }

    public CaptureOverlayViewModel(
        IAppNavigation appNavigation,
        IThemeService themeService,
        IVideoCaptureHandler videoCaptureHandler,
        ITelemetryService telemetryService,
        ITaskEnvironment taskEnvironment,
        ICaptureOverlayActions captureOverlayActions) 
    {
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
        _telemetryService = telemetryService;
        _taskEnvironment = taskEnvironment;
        _captureOverlayActions = captureOverlayActions;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;

        CloseOverlayCommand = new(CloseOverlay);
        GoBackCommand = new(GoBack);
        StartVideoCaptureCommand = new(StartVideoCapture);
        StopVideoCaptureCommand = new(StopVideoCapture);
        ToggleDesktopAudioCommand = new(ToggleDesktopAudio);
    }

    public override void Load(CaptureOverlayViewModelOptions options)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Load, () =>
        {
            ThrowIfNotReadyToLoad();
            StartLoading();

            IsDesktopAudioEnabled = _videoCaptureHandler.IsDesktopAudioEnabled;
            _videoCaptureHandler.DesktopAudioStateChanged += OnDesktopAudioStateChanged;

            _monitorCaptureResult = options.Monitor;
            _captureArea = options.Area;

            base.Load(options);
        });
    }

    private void OnDesktopAudioStateChanged(object? sender, bool value)
    {
        _taskEnvironment.TryExecute(() =>
        {
            IsDesktopAudioEnabled = value;
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
            _captureOverlayActions.Close();
        });
    }

    private void GoBack()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.GoBack, () =>
        {
            _captureOverlayActions.GoBack();
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
                NewCaptureArgs args = new(_monitorCaptureResult.Value, _captureArea.Value);

                _appNavigation.GoToVideoCapture(args);
                _videoCaptureHandler.StartVideoCapture(args);
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
                IVideoFile video = _videoCaptureHandler.StopVideoCapture();
                _appNavigation.GoToVideoEdit(video);
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
            _captureOverlayActions.ToggleDesktopAudio();
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