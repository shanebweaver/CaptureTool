using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Common;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.TaskEnvironment;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Infrastructure.Interfaces.Themes;
using System.Drawing;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class CaptureOverlayViewModel : LoadableViewModelBase<CaptureOverlayViewModelOptions>, ICaptureOverlayViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadCaptureOverlay";
        public static readonly string CloseOverlay = "CloseOverlay";
        public static readonly string GoBack = "GoBack";
        public static readonly string StartVideoCapture = "StartVideoCapture";
        public static readonly string StopVideoCapture = "StopVideoCapture";
        public static readonly string ToggleDesktopAudio = "ToggleDesktopAudio";
        public static readonly string TogglePauseResume = "TogglePauseResume";
    }

    private const string TelemetryContext = "CaptureOverlay";

    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly ITelemetryService _telemetryService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ICaptureOverlayUseCases _captureOverlayActions;

    private MonitorCaptureResult? _monitorCaptureResult;
    private Rectangle? _captureArea;

    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
    private Timer? _timer;
    private DateTime _captureStartTime;
    private TimeSpan _pausedDuration;
    private DateTime? _pauseStartTime;

    public bool IsRecording
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsPaused
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

    public IAppCommand CloseOverlayCommand { get; }
    public IAppCommand GoBackCommand { get; }
    public IAppCommand StartVideoCaptureCommand { get; }
    public IAppCommand StopVideoCaptureCommand { get; }
    public IAppCommand ToggleDesktopAudioCommand { get; }
    public IAppCommand TogglePauseResumeCommand { get; }

    public CaptureOverlayViewModel(
        IAppNavigation appNavigation,
        IThemeService themeService,
        IVideoCaptureHandler videoCaptureHandler,
        ITelemetryService telemetryService,
        ITaskEnvironment taskEnvironment,
        ICaptureOverlayUseCases captureOverlayActions)
    {
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
        _telemetryService = telemetryService;
        _taskEnvironment = taskEnvironment;
        _captureOverlayActions = captureOverlayActions;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        CloseOverlayCommand = commandFactory.Create(ActivityIds.CloseOverlay, CloseOverlay);
        GoBackCommand = commandFactory.Create(ActivityIds.GoBack, GoBack);
        StartVideoCaptureCommand = commandFactory.Create(ActivityIds.StartVideoCapture, StartVideoCapture);
        StopVideoCaptureCommand = commandFactory.Create(ActivityIds.StopVideoCapture, StopVideoCapture);
        ToggleDesktopAudioCommand = commandFactory.Create(ActivityIds.ToggleDesktopAudio, ToggleDesktopAudio);
        TogglePauseResumeCommand = commandFactory.Create(ActivityIds.TogglePauseResume, TogglePauseResume);
    }

    public override void Load(CaptureOverlayViewModelOptions options)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, TelemetryContext, ActivityIds.Load, () =>
        {
            ThrowIfNotReadyToLoad();
            StartLoading();

            IsDesktopAudioEnabled = _videoCaptureHandler.IsDesktopAudioEnabled;
            _videoCaptureHandler.DesktopAudioStateChanged += OnDesktopAudioStateChanged;

            IsPaused = _videoCaptureHandler.IsPaused;
            _videoCaptureHandler.PausedStateChanged += OnPausedStateChanged;

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

    private void OnPausedStateChanged(object? sender, bool value)
    {
        _taskEnvironment.TryExecute(() =>
        {
            IsPaused = value;
        });
    }

    public override void Dispose()
    {
        _videoCaptureHandler.DesktopAudioStateChanged -= OnDesktopAudioStateChanged;
        _videoCaptureHandler.PausedStateChanged -= OnPausedStateChanged;
        
        StopTimer();
        
        // Dispose timer if it exists
        if (_timer != null)
        {
            _timer.Elapsed -= Timer_Elapsed;
            _timer.Dispose();
            _timer = null;
        }
        
        // Explicitly null the MonitorCaptureResult to release the PixelBuffer
        _monitorCaptureResult = null;
        _captureArea = null;
        
        base.Dispose();
    }

    private void CloseOverlay()
    {
        _captureOverlayActions.Close();
    }

    private void GoBack()
    {
        _captureOverlayActions.GoBack();
    }

    private void StartVideoCapture()
    {
        if (!IsRecording && _monitorCaptureResult != null && _captureArea != null)
        {
            IsRecording = true;
            CaptureTime = TimeSpan.Zero;
            _captureStartTime = DateTime.UtcNow;
            _pausedDuration = TimeSpan.Zero;
            _pauseStartTime = null;
            StartTimer();
            NewCaptureArgs args = new(_monitorCaptureResult.Value, _captureArea.Value);

            _captureOverlayActions.StartVideoCapture(args);
        }
        else
        {
            throw new InvalidOperationException("Cannot start video capture. Monitor or capture area is not set.");
        }
    }

    private void StopVideoCapture()
    {
        if (IsRecording)
        {
            IsRecording = false;
            StopTimer();
            _captureOverlayActions.StopVideoCapture();
        }
        else
        {
            throw new InvalidOperationException("Cannot stop video capture. No recording is in progress.");
        }
    }

    private void ToggleDesktopAudio()
    {
        _captureOverlayActions.ToggleDesktopAudio();
    }

    private void TogglePauseResume()
    {
        IsPaused = !IsPaused;

        if (IsPaused)
        {
            _pauseStartTime = DateTime.UtcNow;
        }
        else if (_pauseStartTime.HasValue)
        {
            _pausedDuration += DateTime.UtcNow - _pauseStartTime.Value;
            _pauseStartTime = null;
        }

        _captureOverlayActions.TogglePauseResume();
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
        _taskEnvironment.TryExecute(() =>
        {
            if (IsRecording && !IsPaused)
            {
                CaptureTime = DateTime.UtcNow - _captureStartTime - _pausedDuration;
            }
        });
    }
}