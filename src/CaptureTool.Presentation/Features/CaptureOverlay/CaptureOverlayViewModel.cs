using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CaptureTool.Presentation.Features.CaptureOverlay;

public sealed partial class CaptureOverlayViewModel : LoadableViewModelBase<CaptureOverlayViewModelOptions>
{
    private readonly IStartVideoCaptureUseCase _startVideoCaptureCommand;
    private readonly IStopVideoCaptureUseCase _stopVideoCaptureCommand;
    private readonly IToggleVideoCapturePauseResumeUseCase _toggleVideoCapturePauseResumeCommand;
    private readonly IGetAudioInputSourcesUseCase _getAudioInputSourcesCommand;
    private readonly ISelectAudioInputSourceUseCase _selectAudioInputSourceCommand;
    private readonly IAudioInputDetectionService _audioInputDetectionService;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly ITaskEnvironment _taskEnvironment;

    private MonitorCaptureResult? _monitorCaptureResult;
    private Rectangle? _captureArea;

    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
    private Timer? _timer;
    private DateTime _captureStartTime;
    private TimeSpan _pausedDuration;
    private DateTime? _pauseStartTime;
    private const string DefaultAudioInputSuffix = " (Default)";

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

    public bool IsAudioInputMuted
    {
        get;
        private set => Set(ref field, value);
    }

    public ObservableCollection<AudioInputSource> AudioInputSources { get; }

    public AudioInputSource? SelectedAudioInputSource
    {
        get;
        private set => Set(ref field, value);
    }

    public int SelectedAudioInputSourceIndex
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsAudioInputSelectionAvailable
    {
        get;
        private set => Set(ref field, value);
    }

    public IRelayCommand CloseOverlayCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IAsyncRelayCommand StartVideoCaptureCommand { get; }
    public IAsyncRelayCommand StopVideoCaptureCommand { get; }
    public IRelayCommand ToggleDesktopAudioCommand { get; }
    public IRelayCommand ToggleAudioInputMuteCommand { get; }
    public IAsyncRelayCommand TogglePauseResumeCommand { get; }
    public IRelayCommand<AudioInputSource> SelectAudioInputSourceCommand { get; }

    public CaptureOverlayViewModel(
        ICloseCaptureOverlayUseCase closeOverlayCommand,
        IGoBackFromCaptureOverlayUseCase goBackCommand,
        IStartVideoCaptureUseCase startVideoCaptureCommand,
        IStopVideoCaptureUseCase stopVideoCaptureCommand,
        IToggleVideoCaptureDesktopAudioUseCase toggleVideoCaptureDesktopAudioCommand,
        IToggleVideoCapturePauseResumeUseCase toggleVideoCapturePauseResumeCommand,
        IGetAudioInputSourcesUseCase getAudioInputSourcesCommand,
        ISelectAudioInputSourceUseCase selectAudioInputSourceCommand,
        IAudioInputDetectionService audioInputDetectionService,
        IThemeService themeService,
        IVideoCaptureHandler videoCaptureHandler,
        ITaskEnvironment taskEnvironment)
    {
        _startVideoCaptureCommand = startVideoCaptureCommand;
        _stopVideoCaptureCommand = stopVideoCaptureCommand;
        _toggleVideoCapturePauseResumeCommand = toggleVideoCapturePauseResumeCommand;
        _getAudioInputSourcesCommand = getAudioInputSourcesCommand;
        _selectAudioInputSourceCommand = selectAudioInputSourceCommand;
        _audioInputDetectionService = audioInputDetectionService;
        _videoCaptureHandler = videoCaptureHandler;
        _taskEnvironment = taskEnvironment;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;
        SelectedAudioInputSourceIndex = -1;
        AudioInputSources = [];

        CloseOverlayCommand = closeOverlayCommand.ToRelayCommand(() => new CloseCaptureOverlayRequest());
        GoBackCommand = goBackCommand.ToRelayCommand(() => new GoBackFromCaptureOverlayRequest());
        StartVideoCaptureCommand = new AsyncRelayCommand(StartVideoCaptureAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        StopVideoCaptureCommand = new AsyncRelayCommand(StopVideoCaptureAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleDesktopAudioCommand = toggleVideoCaptureDesktopAudioCommand.ToRelayCommand(() => new ToggleVideoCaptureDesktopAudioRequest());
        ToggleAudioInputMuteCommand = new RelayCommand(ToggleAudioInputMute);
        TogglePauseResumeCommand = new AsyncRelayCommand(TogglePauseResumeAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        SelectAudioInputSourceCommand = new AsyncRelayCommand<AudioInputSource>(SelectAudioInputSourceAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
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

        _audioInputDetectionService.AudioInputSourcesChanged += OnAudioInputSourcesChanged;
        StartAudioInputDetection();

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

    private void OnAudioInputSourcesChanged(object? sender, AudioInputSourcesChangedEventArgs e)
    {
        _taskEnvironment.TryExecute(() =>
        {
            UpdateAudioInputSources(e.Sources);
        });
    }

    private void StartAudioInputDetection()
    {
        try
        {
            _audioInputDetectionService.StartWatching();
            _ = RefreshAudioInputSourcesAsync();
        }
        catch (Exception)
        {
            AudioInputSources.Clear();
            SelectedAudioInputSource = null;
            SelectedAudioInputSourceIndex = -1;
            IsAudioInputSelectionAvailable = false;
        }
    }

    public override void Dispose()
    {
        _videoCaptureHandler.DesktopAudioStateChanged -= OnDesktopAudioStateChanged;
        _videoCaptureHandler.PausedStateChanged -= OnPausedStateChanged;
        _audioInputDetectionService.AudioInputSourcesChanged -= OnAudioInputSourcesChanged;
        try
        {
            _audioInputDetectionService.StopWatching();
        }
        catch (Exception)
        {
            // The capture overlay can still close if the platform watcher is already gone.
        }

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

    private async Task StartVideoCaptureAsync()
    {
        if (IsRecording || _monitorCaptureResult == null || _captureArea == null)
        {
            return;
        }

        IsRecording = true;
        CaptureTime = TimeSpan.Zero;
        _captureStartTime = DateTime.UtcNow;
        _pausedDuration = TimeSpan.Zero;
        _pauseStartTime = null;
        StartTimer();
        NewCaptureArgs args = new(_monitorCaptureResult.Value, _captureArea.Value);

        await _startVideoCaptureCommand.ExecuteAsync(new StartVideoCaptureRequest(args), CancellationToken.None);
    }

    private async Task StopVideoCaptureAsync()
    {
        if (!IsRecording)
        {
            return;
        }

        IsRecording = false;
        StopTimer();
        await _stopVideoCaptureCommand.ExecuteAsync(new StopVideoCaptureRequest(), CancellationToken.None);
    }

    private async Task TogglePauseResumeAsync()
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

        await _toggleVideoCapturePauseResumeCommand.ExecuteAsync(new ToggleVideoCapturePauseResumeRequest(), CancellationToken.None);
    }

    private async Task RefreshAudioInputSourcesAsync()
    {
        GetAudioInputSourcesResponse? response = (await _getAudioInputSourcesCommand.ExecuteAsync(new GetAudioInputSourcesRequest(), CancellationToken.None)).Value;

        _taskEnvironment.TryExecute(() =>
        {
            UpdateAudioInputSources(response?.Sources ?? []);
        });
    }

    private void UpdateAudioInputSources(IReadOnlyList<AudioInputSource> sources)
    {
        AudioInputSources.Clear();
        foreach (AudioInputSource source in sources)
        {
            AudioInputSources.Add(GetDisplayAudioInputSource(source));
        }

        IsAudioInputSelectionAvailable = AudioInputSources.Count > 0;

        if (!IsAudioInputSelectionAvailable)
        {
            SelectedAudioInputSource = null;
            SelectedAudioInputSourceIndex = -1;
            _videoCaptureHandler.SelectAudioInputSource(null);
            return;
        }

        SelectedAudioInputSource = GetAudioInputSourceToSelect();
        SelectedAudioInputSourceIndex = AudioInputSources.IndexOf(SelectedAudioInputSource);
        _videoCaptureHandler.SelectAudioInputSource(SelectedAudioInputSource.Id);
    }

    private AudioInputSource GetAudioInputSourceToSelect()
    {
        return
            AudioInputSources.FirstOrDefault(source => source.IsDefault) ??
            AudioInputSources[0];
    }

    private static AudioInputSource GetDisplayAudioInputSource(AudioInputSource source)
    {
        if (!source.IsDefault || source.DisplayName.EndsWith(DefaultAudioInputSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        return source with { DisplayName = $"{source.DisplayName}{DefaultAudioInputSuffix}" };
    }

    private void ToggleAudioInputMute()
    {
        IsAudioInputMuted = !IsAudioInputMuted;
        _videoCaptureHandler.SetIsAudioInputMuted(IsAudioInputMuted);
    }

    private async Task SelectAudioInputSourceAsync(AudioInputSource? source)
    {
        if (source == null)
        {
            return;
        }

        SelectAudioInputSourceResponse? response = (await _selectAudioInputSourceCommand.ExecuteAsync(
            new SelectAudioInputSourceRequest(source.Id),
            CancellationToken.None)).Value;

        if (response?.IsAvailable == true)
        {
            SelectedAudioInputSource = source;
            SelectedAudioInputSourceIndex = AudioInputSources.IndexOf(source);
        }
        else if (response?.WasRemoved == true)
        {
            await RefreshAudioInputSourcesAsync();
        }
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
