using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
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
    private readonly IAudioInputSelectionFeatureAvailability _audioInputSelectionFeatureAvailability;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ITelemetryService _telemetryService;

    private MonitorCaptureResult? _monitorCaptureResult;
    private Rectangle? _captureArea;

    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
    private Timer? _timer;
    private DateTime _captureStartTime;
    private TimeSpan _pausedDuration;
    private DateTime? _pauseStartTime;
    private bool _hasInitializedAudioInputSelection;

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

    public bool IsAudioInputSelectionFeatureEnabled
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
        IAudioInputSelectionFeatureAvailability audioInputSelectionFeatureAvailability,
        IThemeService themeService,
        IVideoCaptureHandler videoCaptureHandler,
        ITaskEnvironment taskEnvironment,
        ITelemetryService telemetryService)
    {
        _startVideoCaptureCommand = startVideoCaptureCommand;
        _stopVideoCaptureCommand = stopVideoCaptureCommand;
        _toggleVideoCapturePauseResumeCommand = toggleVideoCapturePauseResumeCommand;
        _getAudioInputSourcesCommand = getAudioInputSourcesCommand;
        _selectAudioInputSourceCommand = selectAudioInputSourceCommand;
        _audioInputDetectionService = audioInputDetectionService;
        _audioInputSelectionFeatureAvailability = audioInputSelectionFeatureAvailability;
        _videoCaptureHandler = videoCaptureHandler;
        _taskEnvironment = taskEnvironment;
        _telemetryService = telemetryService;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;
        SelectedAudioInputSourceIndex = -1;
        AudioInputSources = [];

        CloseOverlayCommand = closeOverlayCommand.ToRelayCommand(() => new CloseCaptureOverlayRequest(), telemetryService);
        GoBackCommand = goBackCommand.ToRelayCommand(() => new GoBackFromCaptureOverlayRequest(), telemetryService);
        StartVideoCaptureCommand = new AsyncRelayCommand(StartVideoCaptureAsync);
        StopVideoCaptureCommand = new AsyncRelayCommand(StopVideoCaptureAsync);
        ToggleDesktopAudioCommand = toggleVideoCaptureDesktopAudioCommand.ToRelayCommand(() => new ToggleVideoCaptureDesktopAudioRequest(), telemetryService);
        ToggleAudioInputMuteCommand = new RelayCommand(ToggleAudioInputMute);
        TogglePauseResumeCommand = new AsyncRelayCommand(TogglePauseResumeAsync);
        SelectAudioInputSourceCommand = new RelayCommand<AudioInputSource>(SelectAudioInputSource);
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

        IsAudioInputSelectionFeatureEnabled = _audioInputSelectionFeatureAvailability.IsAudioInputSelectionEnabled;
        if (IsAudioInputSelectionFeatureEnabled)
        {
            _audioInputDetectionService.AudioInputSourcesChanged += OnAudioInputSourcesChanged;
            StartAudioInputDetection();
        }

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
            UpdateAudioInputSources(e.Sources, e.Reason, e.AffectedSourceId);
        });
    }

    private void StartAudioInputDetection()
    {
        try
        {
            _audioInputDetectionService.StartWatching();
            _ = RefreshAudioInputSourcesAsync();
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(StartAudioInputDetection), exception);
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
        if (IsAudioInputSelectionFeatureEnabled)
        {
            _audioInputDetectionService.AudioInputSourcesChanged -= OnAudioInputSourcesChanged;
            try
            {
                _audioInputDetectionService.StopWatching();
            }
            catch (Exception exception)
            {
                _telemetryService.ActivityError(nameof(Dispose), exception);
                // The capture overlay can still close if the platform watcher is already gone.
            }
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
        try
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

                await _startVideoCaptureCommand.ExecuteAsync(new StartVideoCaptureRequest(args), CancellationToken.None);
            }
            else
            {
                throw new InvalidOperationException("Cannot start video capture. Monitor or capture area is not set.");
            }
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(StartVideoCaptureAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(StartVideoCaptureAsync), exception);
        }
    }

    private async Task StopVideoCaptureAsync()
    {
        try
        {
            if (IsRecording)
            {
                IsRecording = false;
                StopTimer();
                await _stopVideoCaptureCommand.ExecuteAsync(new StopVideoCaptureRequest(), CancellationToken.None);
            }
            else
            {
                throw new InvalidOperationException("Cannot stop video capture. No recording is in progress.");
            }
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(StopVideoCaptureAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(StopVideoCaptureAsync), exception);
        }
    }

    private async Task TogglePauseResumeAsync()
    {
        try
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
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(TogglePauseResumeAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(TogglePauseResumeAsync), exception);
        }
    }

    private async Task RefreshAudioInputSourcesAsync()
    {
        try
        {
            GetAudioInputSourcesResponse response = await _getAudioInputSourcesCommand.ExecuteAsync(new GetAudioInputSourcesRequest(), CancellationToken.None);

            _taskEnvironment.TryExecute(() =>
            {
                UpdateAudioInputSources(response.Sources, AudioInputSourcesChangeReason.EnumerationCompleted);
            });
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(RefreshAudioInputSourcesAsync), exception);
            _taskEnvironment.TryExecute(() =>
            {
                AudioInputSources.Clear();
                SelectedAudioInputSource = null;
                SelectedAudioInputSourceIndex = -1;
                IsAudioInputSelectionAvailable = false;
            });
        }
    }

    private void UpdateAudioInputSources(
        IReadOnlyList<AudioInputSource> sources,
        AudioInputSourcesChangeReason reason,
        string? affectedSourceId = null)
    {
        string? selectedSourceId = SelectedAudioInputSource?.Id;
        bool selectedSourceRemoved =
            reason is AudioInputSourcesChangeReason.Removed &&
            !string.IsNullOrWhiteSpace(selectedSourceId) &&
            string.Equals(selectedSourceId, affectedSourceId, StringComparison.OrdinalIgnoreCase);

        AudioInputSources.Clear();
        foreach (AudioInputSource source in sources)
        {
            AudioInputSources.Add(source);
        }

        IsAudioInputSelectionAvailable = AudioInputSources.Count > 0;

        if (!IsAudioInputSelectionAvailable)
        {
            SelectedAudioInputSource = null;
            SelectedAudioInputSourceIndex = -1;
            _hasInitializedAudioInputSelection = false;
            return;
        }

        SelectedAudioInputSource = GetAudioInputSourceToSelect(selectedSourceId, selectedSourceRemoved);
        SelectedAudioInputSourceIndex = AudioInputSources.IndexOf(SelectedAudioInputSource);

        _hasInitializedAudioInputSelection = true;
    }

    private AudioInputSource GetAudioInputSourceToSelect(string? selectedSourceId, bool selectedSourceRemoved)
    {
        if (_hasInitializedAudioInputSelection && !selectedSourceRemoved)
        {
            AudioInputSource? existingSelection = AudioInputSources.FirstOrDefault(source => string.Equals(source.Id, selectedSourceId, StringComparison.OrdinalIgnoreCase));
            if (existingSelection != null)
            {
                return existingSelection;
            }
        }

        return
            AudioInputSources.FirstOrDefault(source => source.IsDefault) ??
            AudioInputSources[0];
    }

    private void ToggleAudioInputMute()
    {
        IsAudioInputMuted = !IsAudioInputMuted;
    }

    private async void SelectAudioInputSource(AudioInputSource? source)
    {
        if (source == null)
        {
            return;
        }

        try
        {
            SelectAudioInputSourceResponse response = await _selectAudioInputSourceCommand.ExecuteAsync(
                new SelectAudioInputSourceRequest(source.Id),
                CancellationToken.None);

            if (response.IsAvailable)
            {
                SelectedAudioInputSource = source;
                SelectedAudioInputSourceIndex = AudioInputSources.IndexOf(source);
            }
            else if (response.WasRemoved)
            {
                await RefreshAudioInputSourcesAsync();
            }
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(SelectAudioInputSource), exception);
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
