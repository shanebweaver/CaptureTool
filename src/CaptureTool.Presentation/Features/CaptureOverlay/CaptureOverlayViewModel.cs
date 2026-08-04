using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.PrepareVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.Capture.Video.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.UseCases;
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
    private const string StartRecordingFailedMessageResourceKey = "CaptureOverlay_StartRecordingFailedMessage";
    private const string StartRecordingFailedMessageFallback = "Recording couldn't start. Try again.";
    private const string RecordingUnsupportedMessageResourceKey = "CaptureOverlay_RecordingUnsupportedMessage";
    private const string RecordingUnsupportedMessageFallback = "Screen recording isn't supported on this device.";

    private readonly IStartVideoCaptureUseCase _startVideoCaptureCommand;
    private readonly ICancelVideoCaptureUseCase _cancelVideoCaptureCommand;
    private readonly IStopVideoCaptureUseCase _stopVideoCaptureCommand;
    private readonly IToggleVideoCapturePauseResumeUseCase _toggleVideoCapturePauseResumeCommand;
    private readonly IPrepareVideoCaptureUseCase _prepareVideoCaptureCommand;
    private readonly IGetAudioInputSourcesUseCase _getAudioInputSourcesCommand;
    private readonly ISelectAudioInputSourceUseCase _selectAudioInputSourceCommand;
    private readonly ISetVideoCaptureAudioInputMutedUseCase _setVideoCaptureAudioInputMutedCommand;
    private readonly IAudioInputDetectionService _audioInputDetectionService;
    private readonly IVideoCaptureState _videoCaptureState;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ILocalizationService _localizationService;

    private NewCaptureArgs? _captureArgs;

    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DefaultRecordingStartTimeout = TimeSpan.FromSeconds(5);
    private Timer? _timer;
    private CancellationTokenSource? _recordingStartCancellationTokenSource;
    private TaskCompletionSource? _recordingStartedCompletionSource;
    private readonly TimeSpan _recordingStartTimeout;
    private bool _isDisposed;
    private DateTime _captureStartTime;
    private TimeSpan _pausedDuration;
    private DateTime? _pauseStartTime;
    private const string DefaultAudioInputSuffix = " (Default)";

    public bool IsRecording
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsStarting
    {
        get;
        private set => Set(ref field, value);
    }

    public string RecordingErrorMessage
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(HasRecordingError));
            }
        }
    } = string.Empty;

    public bool HasRecordingError => !string.IsNullOrWhiteSpace(RecordingErrorMessage);

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
    public IAsyncRelayCommand ToggleAudioInputMuteCommand { get; }
    public IAsyncRelayCommand TogglePauseResumeCommand { get; }
    public IAsyncRelayCommand<AudioInputSource> SelectAudioInputSourceCommand { get; }
    public IRelayCommand DismissRecordingErrorCommand { get; }

    public CaptureOverlayViewModel(
        ICloseCaptureOverlayUseCase closeOverlayCommand,
        IGoBackFromCaptureOverlayUseCase goBackCommand,
        IStartVideoCaptureUseCase startVideoCaptureCommand,
        ICancelVideoCaptureUseCase cancelVideoCaptureCommand,
        IStopVideoCaptureUseCase stopVideoCaptureCommand,
        IToggleVideoCaptureDesktopAudioUseCase toggleVideoCaptureDesktopAudioCommand,
        IToggleVideoCapturePauseResumeUseCase toggleVideoCapturePauseResumeCommand,
        IPrepareVideoCaptureUseCase prepareVideoCaptureCommand,
        IGetAudioInputSourcesUseCase getAudioInputSourcesCommand,
        ISelectAudioInputSourceUseCase selectAudioInputSourceCommand,
        ISetVideoCaptureAudioInputMutedUseCase setVideoCaptureAudioInputMutedCommand,
        IAudioInputDetectionService audioInputDetectionService,
        IThemeService themeService,
        IVideoCaptureState videoCaptureState,
        ITaskEnvironment taskEnvironment,
        ILocalizationService localizationService,
        TimeSpan? recordingStartTimeout = null)
    {
        _startVideoCaptureCommand = startVideoCaptureCommand;
        _cancelVideoCaptureCommand = cancelVideoCaptureCommand;
        _stopVideoCaptureCommand = stopVideoCaptureCommand;
        _toggleVideoCapturePauseResumeCommand = toggleVideoCapturePauseResumeCommand;
        _prepareVideoCaptureCommand = prepareVideoCaptureCommand;
        _getAudioInputSourcesCommand = getAudioInputSourcesCommand;
        _selectAudioInputSourceCommand = selectAudioInputSourceCommand;
        _setVideoCaptureAudioInputMutedCommand = setVideoCaptureAudioInputMutedCommand;
        _audioInputDetectionService = audioInputDetectionService;
        _videoCaptureState = videoCaptureState;
        _taskEnvironment = taskEnvironment;
        _localizationService = localizationService;
        _recordingStartTimeout = recordingStartTimeout ?? DefaultRecordingStartTimeout;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;
        SelectedAudioInputSourceIndex = -1;
        AudioInputSources = [];

        CloseOverlayCommand = closeOverlayCommand.ToRelayCommand(() => new CloseCaptureOverlayRequest());
        GoBackCommand = goBackCommand.ToRelayCommand(() => new GoBackFromCaptureOverlayRequest());
        StartVideoCaptureCommand = new AsyncRelayCommand(StartVideoCaptureAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        StopVideoCaptureCommand = new AsyncRelayCommand(StopVideoCaptureAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleDesktopAudioCommand = toggleVideoCaptureDesktopAudioCommand.ToRelayCommand(() => new ToggleVideoCaptureDesktopAudioRequest());
        ToggleAudioInputMuteCommand = new AsyncRelayCommand(ToggleAudioInputMuteAsync);
        TogglePauseResumeCommand = new AsyncRelayCommand(TogglePauseResumeAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        SelectAudioInputSourceCommand = new AsyncRelayCommand<AudioInputSource>(SelectAudioInputSourceAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        DismissRecordingErrorCommand = new RelayCommand(DismissRecordingError);
    }

    public override void Load(CaptureOverlayViewModelOptions options)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        _prepareVideoCaptureCommand.ExecuteAsync(new PrepareVideoCaptureRequest(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        IsDesktopAudioEnabled = _videoCaptureState.IsDesktopAudioEnabled;
        IsAudioInputMuted = _videoCaptureState.IsAudioInputMuted;
        _videoCaptureState.RecordingStarted += OnRecordingStarted;
        _videoCaptureState.DesktopAudioStateChanged += OnDesktopAudioStateChanged;
        _videoCaptureState.AudioInputMutedStateChanged += OnAudioInputMutedStateChanged;
        _videoCaptureState.AudioInputSourceChanged += OnAudioInputSourceChanged;

        IsPaused = _videoCaptureState.IsPaused;
        _videoCaptureState.PausedStateChanged += OnPausedStateChanged;

        _audioInputDetectionService.AudioInputSourcesChanged += OnAudioInputSourcesChanged;
        StartAudioInputDetection();

        _captureArgs = options.CaptureArgs;

        base.Load(options);
    }

    private void OnDesktopAudioStateChanged(object? sender, bool value)
    {
        _taskEnvironment.TryExecute(() =>
        {
            IsDesktopAudioEnabled = value;
        });
    }

    private void OnAudioInputMutedStateChanged(object? sender, bool value)
    {
        _taskEnvironment.TryExecute(() => IsAudioInputMuted = value);
    }

    private void OnAudioInputSourceChanged(object? sender, string? sourceId)
    {
        _taskEnvironment.TryExecute(() => ApplySelectedAudioInputSource(sourceId));
    }

    private void OnRecordingStarted(object? sender, EventArgs e)
    {
        _taskEnvironment.TryExecute(() =>
        {
            if (_isDisposed || (!IsStarting && !IsRecording))
            {
                return;
            }

            IsStarting = false;
            IsRecording = true;
            _captureStartTime = DateTime.UtcNow;
            _pausedDuration = TimeSpan.Zero;
            _pauseStartTime = IsPaused ? _captureStartTime : null;
            CaptureTime = TimeSpan.Zero;
            StartTimer();
            _recordingStartedCompletionSource?.TrySetResult();
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
        _isDisposed = true;
        IsStarting = false;
        CancelRecordingStartWait();
        _videoCaptureState.RecordingStarted -= OnRecordingStarted;
        _videoCaptureState.DesktopAudioStateChanged -= OnDesktopAudioStateChanged;
        _videoCaptureState.AudioInputMutedStateChanged -= OnAudioInputMutedStateChanged;
        _videoCaptureState.AudioInputSourceChanged -= OnAudioInputSourceChanged;
        _videoCaptureState.PausedStateChanged -= OnPausedStateChanged;
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

        // Explicitly null the capture arguments to release the monitor PixelBuffer.
        _captureArgs = null;

        base.Dispose();
    }

    private async Task StartVideoCaptureAsync()
    {
        if (IsRecording || IsStarting || _captureArgs == null)
        {
            return;
        }

        DismissRecordingError();

        IsStarting = true;
        var recordingStartCancellationTokenSource = new CancellationTokenSource();
        var recordingStartedCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _recordingStartCancellationTokenSource = recordingStartCancellationTokenSource;
        _recordingStartedCompletionSource = recordingStartedCompletionSource;
        CaptureTime = TimeSpan.Zero;
        _pausedDuration = TimeSpan.Zero;
        _pauseStartTime = null;

        UseCaseResponse<StartVideoCaptureResponse> response;
        try
        {
            response = await _startVideoCaptureCommand.ExecuteAsync(
                new StartVideoCaptureRequest(_captureArgs.Value),
                CancellationToken.None);
        }
        catch (Exception)
        {
            HandleRecordingStartFailure();
            ClearRecordingStartWait(recordingStartCancellationTokenSource);
            return;
        }

        if (response.Result != UseCaseResult.Succeeded || response.Value?.Succeeded != true)
        {
            bool isUnsupported = response.Value?.FailureReason == StartVideoCaptureFailureReason.NotSupported;
            HandleRecordingStartFailure(
                showMessage: response.Result != UseCaseResult.Cancelled && !isUnsupported);
            if (isUnsupported)
            {
                ShowRecordingError(RecordingUnsupportedMessageResourceKey, RecordingUnsupportedMessageFallback);
            }
            ClearRecordingStartWait(recordingStartCancellationTokenSource);
            return;
        }

        try
        {
            await recordingStartedCompletionSource.Task.WaitAsync(
                _recordingStartTimeout,
                recordingStartCancellationTokenSource.Token);
        }
        catch (TimeoutException)
        {
            if (!_isDisposed &&
                ReferenceEquals(_recordingStartCancellationTokenSource, recordingStartCancellationTokenSource) &&
                IsStarting)
            {
                // Mark the attempt inactive before cancelling so a late native callback is ignored.
                IsStarting = false;
                try
                {
                    await _cancelVideoCaptureCommand.ExecuteAsync(
                        new CancelVideoCaptureRequest(
                            SkipConfirmation: true,
                            Reason: CancelVideoCaptureReason.StartTimeout),
                        CancellationToken.None);
                }
                catch (Exception)
                {
                    // The start failure remains the actionable error for the user.
                }

                HandleRecordingStartFailure();
            }
        }
        catch (OperationCanceledException)
        {
            // Disposal or another terminal start outcome cancelled the wait.
        }
        finally
        {
            ClearRecordingStartWait(recordingStartCancellationTokenSource);
        }
    }

    private void HandleRecordingStartFailure(bool showMessage = true)
    {
        IsStarting = false;
        IsRecording = false;
        IsPaused = false;
        CaptureTime = TimeSpan.Zero;
        _pausedDuration = TimeSpan.Zero;
        _pauseStartTime = null;
        StopTimer();

        if (showMessage)
        {
            ShowRecordingError(StartRecordingFailedMessageResourceKey, StartRecordingFailedMessageFallback);
        }
    }

    private void ShowRecordingError(string resourceKey, string fallback)
    {
        string message;
        try
        {
            message = _localizationService.GetString(resourceKey);
        }
        catch (Exception)
        {
            message = string.Empty;
        }

        RecordingErrorMessage = string.IsNullOrWhiteSpace(message) ? fallback : message;
    }

    private void DismissRecordingError()
    {
        RecordingErrorMessage = string.Empty;
    }

    private void CancelRecordingStartWait()
    {
        _recordingStartCancellationTokenSource?.Cancel();
        _recordingStartedCompletionSource?.TrySetCanceled();
    }

    private void ClearRecordingStartWait(CancellationTokenSource cancellationTokenSource)
    {
        if (!ReferenceEquals(_recordingStartCancellationTokenSource, cancellationTokenSource))
        {
            return;
        }

        _recordingStartCancellationTokenSource = null;
        _recordingStartedCompletionSource = null;
        cancellationTokenSource.Dispose();
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
        string? selectedAudioInputSourceId = SelectedAudioInputSource?.Id;
        AudioInputSources.Clear();
        foreach (AudioInputSource source in sources)
        {
            AudioInputSources.Add(GetDisplayAudioInputSource(source));
        }

        IsAudioInputSelectionAvailable = AudioInputSources.Count > 0;

        if (!IsAudioInputSelectionAvailable)
        {
            ApplySelectedAudioInputSource(null);
            _ = RequestAudioInputSourceSelectionAsync(null);
            return;
        }

        ApplySelectedAudioInputSource(_videoCaptureState.SelectedAudioInputSourceId);
        AudioInputSource sourceToSelect = GetAudioInputSourceToSelect(selectedAudioInputSourceId);
        _ = RequestAudioInputSourceSelectionAsync(sourceToSelect.Id);
    }

    private AudioInputSource GetAudioInputSourceToSelect(string? selectedAudioInputSourceId)
    {
        return
            AudioInputSources.FirstOrDefault(source => string.Equals(source.Id, selectedAudioInputSourceId, StringComparison.OrdinalIgnoreCase)) ??
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

    private async Task ToggleAudioInputMuteAsync()
    {
        await _setVideoCaptureAudioInputMutedCommand.ExecuteAsync(
            new SetVideoCaptureAudioInputMutedRequest(!IsAudioInputMuted),
            CancellationToken.None);
    }

    private async Task<SelectAudioInputSourceResponse?> RequestAudioInputSourceSelectionAsync(string? sourceId)
    {
        return (await _selectAudioInputSourceCommand.ExecuteAsync(
            new SelectAudioInputSourceRequest(sourceId),
            CancellationToken.None)).Value;
    }

    private async Task SelectAudioInputSourceAsync(AudioInputSource? source)
    {
        if (source == null)
        {
            return;
        }

        SelectAudioInputSourceResponse? response = await RequestAudioInputSourceSelectionAsync(source.Id);

        if (response?.WasRemoved == true)
        {
            await RefreshAudioInputSourcesAsync();
        }
    }

    private void ApplySelectedAudioInputSource(string? sourceId)
    {
        SelectedAudioInputSource = AudioInputSources.FirstOrDefault(source =>
            string.Equals(source.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        SelectedAudioInputSourceIndex = SelectedAudioInputSource is null
            ? -1
            : AudioInputSources.IndexOf(SelectedAudioInputSource);
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
