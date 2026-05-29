using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.TaskEnvironment;
using CaptureTool.Infrastructure.Abstractions.Themes;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class CaptureOverlayViewModel : LoadableViewModelBase<CaptureOverlayViewModelOptions>
{
    private readonly IUseCase<StartVideoCaptureRequest, StartVideoCaptureResponse> _startVideoCaptureCommand;
    private readonly IUseCase<StopVideoCaptureRequest, StopVideoCaptureResponse> _stopVideoCaptureCommand;
    private readonly IUseCase<ToggleVideoCapturePauseResumeRequest, ToggleVideoCapturePauseResumeResponse> _toggleVideoCapturePauseResumeCommand;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly ITaskEnvironment _taskEnvironment;

    private MonitorCaptureResult? _monitorCaptureResult;
    private Rectangle? _captureArea;

    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
    private Timer? _timer;
    private DateTime _captureStartTime;
    private TimeSpan _pausedDuration;
    private DateTime? _pauseStartTime;

    public bool IsRecording
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsPaused
    {
        get;
        private set => Set(ref field, value);
    }

    public TimeSpan CaptureTime
    {
        get;
        private set => Set(ref field, value);
    }

    public AppTheme CurrentAppTheme
    {
        get;
        private set => Set(ref field, value);
    }

    public AppTheme DefaultAppTheme
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsDesktopAudioEnabled
    {
        get;
        private set => Set(ref field, value);
    }

    public IRelayCommand CloseOverlayCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IRelayCommand StartVideoCaptureCommand { get; }
    public IRelayCommand StopVideoCaptureCommand { get; }
    public IRelayCommand ToggleDesktopAudioCommand { get; }
    public IRelayCommand TogglePauseResumeCommand { get; }

    public CaptureOverlayViewModel(
        IUseCase<CloseCaptureOverlayRequest, CloseCaptureOverlayResponse> closeOverlayCommand,
        IUseCase<GoBackFromCaptureOverlayRequest, GoBackFromCaptureOverlayResponse> goBackCommand,
        IUseCase<StartVideoCaptureRequest, StartVideoCaptureResponse> startVideoCaptureCommand,
        IUseCase<StopVideoCaptureRequest, StopVideoCaptureResponse> stopVideoCaptureCommand,
        IUseCase<ToggleVideoCaptureDesktopAudioRequest, ToggleVideoCaptureDesktopAudioResponse> toggleVideoCaptureDesktopAudioCommand,
        IUseCase<ToggleVideoCapturePauseResumeRequest, ToggleVideoCapturePauseResumeResponse> toggleVideoCapturePauseResumeCommand,
        IThemeService themeService,
        IVideoCaptureHandler videoCaptureHandler,
        ITaskEnvironment taskEnvironment)
    {
        _startVideoCaptureCommand = startVideoCaptureCommand;
        _stopVideoCaptureCommand = stopVideoCaptureCommand;
        _toggleVideoCapturePauseResumeCommand = toggleVideoCapturePauseResumeCommand;
        _videoCaptureHandler = videoCaptureHandler;
        _taskEnvironment = taskEnvironment;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;

        CloseOverlayCommand = closeOverlayCommand.ToRelayCommand(() => new CloseCaptureOverlayRequest());
        GoBackCommand = goBackCommand.ToRelayCommand(() => new GoBackFromCaptureOverlayRequest());
        StartVideoCaptureCommand = new RelayCommand(StartVideoCapture);
        StopVideoCaptureCommand = new RelayCommand(StopVideoCapture);
        ToggleDesktopAudioCommand = toggleVideoCaptureDesktopAudioCommand.ToRelayCommand(() => new ToggleVideoCaptureDesktopAudioRequest()); 
        TogglePauseResumeCommand = new RelayCommand(TogglePauseResume);
    }

    public override void Load(CaptureOverlayViewModelOptions options)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        _videoCaptureHandler.PrepareForVideoCapture();

        IsDesktopAudioEnabled = _videoCaptureHandler.IsDesktopAudioEnabled;
        _videoCaptureHandler.DesktopAudioStateChanged += OnDesktopAudioStateChanged;

        IsPaused = _videoCaptureHandler.IsPaused;
        _videoCaptureHandler.PausedStateChanged += OnPausedStateChanged;

        _monitorCaptureResult = options.Monitor;
        _captureArea = options.Area;

        base.Load(options);
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

            _startVideoCaptureCommand.ExecuteAsync(new StartVideoCaptureRequest(args)).GetAwaiter().GetResult();
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
            _stopVideoCaptureCommand.ExecuteAsync(new StopVideoCaptureRequest()).GetAwaiter().GetResult();
        }
        else
        {
            throw new InvalidOperationException("Cannot stop video capture. No recording is in progress.");
        }
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

        _toggleVideoCapturePauseResumeCommand.ExecuteAsync(new ToggleVideoCapturePauseResumeRequest()).GetAwaiter().GetResult();
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