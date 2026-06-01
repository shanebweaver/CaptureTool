using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Audio;
using CaptureTool.Infrastructure.Abstractions.TaskEnvironment;
using CaptureTool.Infrastructure.Abstractions.Themes;
using CaptureTool.Infrastructure.ViewModels;
using CaptureTool.Presentation.Shared.Commands;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CaptureTool.Presentation.Features.CaptureOverlay;

public sealed partial class CaptureOverlayViewModel : LoadableViewModelBase<CaptureOverlayViewModelOptions>
{
    private readonly StartVideoCaptureUseCase _startVideoCaptureCommand;
    private readonly StopVideoCaptureUseCase _stopVideoCaptureCommand;
    private readonly ToggleVideoCapturePauseResumeUseCase _toggleVideoCapturePauseResumeCommand;
    private readonly GetAudioInputSourcesUseCase _getAudioInputSourcesCommand;
    private readonly SelectAudioInputSourceUseCase _selectAudioInputSourceCommand;
    private readonly IAudioInputDetectionService _audioInputDetectionService;
    private readonly IFeatureManager _featureManager;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly ITaskEnvironment _taskEnvironment;

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

    public ObservableCollection<AudioInputSource> AudioInputSources { get; } = [];

    public AudioInputSource? SelectedAudioInputSource
    {
        get;
        private set => Set(ref field, value);
    }

    public int SelectedAudioInputSourceIndex
    {
        get;
        private set => Set(ref field, value);
    } = -1;

    public bool IsAudioInputSelectionAvailable
    {
        get;
        private set => Set(ref field, value);
    }

    public string AudioInputSelectionStatus
    {
        get;
        private set => Set(ref field, value);
    } = "Loading audio inputs";

    public IRelayCommand CloseOverlayCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IRelayCommand StartVideoCaptureCommand { get; }
    public IRelayCommand StopVideoCaptureCommand { get; }
    public IRelayCommand ToggleDesktopAudioCommand { get; }
    public IRelayCommand ToggleAudioInputMuteCommand { get; }
    public IRelayCommand TogglePauseResumeCommand { get; }
    public IRelayCommand<AudioInputSource> SelectAudioInputSourceCommand { get; }

    public CaptureOverlayViewModel(
        CloseCaptureOverlayUseCase closeOverlayCommand,
        GoBackFromCaptureOverlayUseCase goBackCommand,
        StartVideoCaptureUseCase startVideoCaptureCommand,
        StopVideoCaptureUseCase stopVideoCaptureCommand,
        ToggleVideoCaptureDesktopAudioUseCase toggleVideoCaptureDesktopAudioCommand,
        ToggleVideoCapturePauseResumeUseCase toggleVideoCapturePauseResumeCommand,
        GetAudioInputSourcesUseCase getAudioInputSourcesCommand,
        SelectAudioInputSourceUseCase selectAudioInputSourceCommand,
        IAudioInputDetectionService audioInputDetectionService,
        IFeatureManager featureManager,
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
        _featureManager = featureManager;
        _videoCaptureHandler = videoCaptureHandler;
        _taskEnvironment = taskEnvironment;

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;

        CloseOverlayCommand = closeOverlayCommand.ToRelayCommand(() => new CloseCaptureOverlayRequest());
        GoBackCommand = goBackCommand.ToRelayCommand(() => new GoBackFromCaptureOverlayRequest());
        StartVideoCaptureCommand = new RelayCommand(StartVideoCapture);
        StopVideoCaptureCommand = new RelayCommand(StopVideoCapture);
        ToggleDesktopAudioCommand = toggleVideoCaptureDesktopAudioCommand.ToRelayCommand(() => new ToggleVideoCaptureDesktopAudioRequest());
        ToggleAudioInputMuteCommand = new RelayCommand(ToggleAudioInputMute);
        TogglePauseResumeCommand = new RelayCommand(TogglePauseResume);
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

        IsAudioInputSelectionFeatureEnabled = _featureManager.IsEnabled(AppFeatures.Feature_AudioInputSelection);
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
        catch
        {
            AudioInputSources.Clear();
            SelectedAudioInputSource = null;
            SelectedAudioInputSourceIndex = -1;
            IsAudioInputSelectionAvailable = false;
            AudioInputSelectionStatus = "Audio inputs unavailable";
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
            catch
            {
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
        catch
        {
            _taskEnvironment.TryExecute(() =>
            {
                AudioInputSources.Clear();
                SelectedAudioInputSource = null;
                SelectedAudioInputSourceIndex = -1;
                IsAudioInputSelectionAvailable = false;
                AudioInputSelectionStatus = "Audio inputs unavailable";
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
            AudioInputSelectionStatus = "No audio inputs found";
            _hasInitializedAudioInputSelection = false;
            return;
        }

        SelectedAudioInputSource = GetAudioInputSourceToSelect(selectedSourceId, selectedSourceRemoved);
        SelectedAudioInputSourceIndex = AudioInputSources.IndexOf(SelectedAudioInputSource);

        _hasInitializedAudioInputSelection = true;

        AudioInputSelectionStatus = selectedSourceRemoved
            ? "Selected audio input was removed"
            : string.Empty;
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
                AudioInputSelectionStatus = string.Empty;
            }
            else if (response.WasRemoved)
            {
                AudioInputSelectionStatus = "Selected audio input was removed";
                await RefreshAudioInputSourcesAsync();
            }
        }
        catch
        {
            AudioInputSelectionStatus = "Audio input selection unavailable";
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
