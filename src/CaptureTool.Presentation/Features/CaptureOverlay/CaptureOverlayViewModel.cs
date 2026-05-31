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
    private readonly IUseCase<StartVideoCaptureRequest, StartVideoCaptureResponse> _startVideoCaptureCommand;
    private readonly IUseCase<StopVideoCaptureRequest, StopVideoCaptureResponse> _stopVideoCaptureCommand;
    private readonly IUseCase<ToggleVideoCapturePauseResumeRequest, ToggleVideoCapturePauseResumeResponse> _toggleVideoCapturePauseResumeCommand;
    private readonly IUseCase<GetAudioInputSourcesRequest, GetAudioInputSourcesResponse> _getAudioInputSourcesCommand;
    private readonly IUseCase<SelectAudioInputSourceRequest, SelectAudioInputSourceResponse> _selectAudioInputSourceCommand;
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

    public ObservableCollection<AudioInputSource> AudioInputSources { get; } = [];

    public AudioInputSource? SelectedAudioInputSource
    {
        get;
        private set => Set(ref field, value);
    }

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
    public IRelayCommand TogglePauseResumeCommand { get; }
    public IRelayCommand<AudioInputSource> SelectAudioInputSourceCommand { get; }

    public CaptureOverlayViewModel(
        IUseCase<CloseCaptureOverlayRequest, CloseCaptureOverlayResponse> closeOverlayCommand,
        IUseCase<GoBackFromCaptureOverlayRequest, GoBackFromCaptureOverlayResponse> goBackCommand,
        IUseCase<StartVideoCaptureRequest, StartVideoCaptureResponse> startVideoCaptureCommand,
        IUseCase<StopVideoCaptureRequest, StopVideoCaptureResponse> stopVideoCaptureCommand,
        IUseCase<ToggleVideoCaptureDesktopAudioRequest, ToggleVideoCaptureDesktopAudioResponse> toggleVideoCaptureDesktopAudioCommand,
        IUseCase<ToggleVideoCapturePauseResumeRequest, ToggleVideoCapturePauseResumeResponse> toggleVideoCapturePauseResumeCommand,
        IUseCase<GetAudioInputSourcesRequest, GetAudioInputSourcesResponse> getAudioInputSourcesCommand,
        IUseCase<SelectAudioInputSourceRequest, SelectAudioInputSourceResponse> selectAudioInputSourceCommand,
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

        CloseOverlayCommand = closeOverlayCommand.ToRelayCommand(() => new CloseCaptureOverlayRequest());
        GoBackCommand = goBackCommand.ToRelayCommand(() => new GoBackFromCaptureOverlayRequest());
        StartVideoCaptureCommand = new RelayCommand(StartVideoCapture);
        StopVideoCaptureCommand = new RelayCommand(StopVideoCapture);
        ToggleDesktopAudioCommand = toggleVideoCaptureDesktopAudioCommand.ToRelayCommand(() => new ToggleVideoCaptureDesktopAudioRequest());
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
            IsAudioInputSelectionAvailable = false;
            AudioInputSelectionStatus = "Audio inputs unavailable";
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
        catch
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
            AudioInputSelectionStatus = "No audio inputs found";
            return;
        }

        SelectedAudioInputSource =
            AudioInputSources.FirstOrDefault(source => string.Equals(source.Id, selectedSourceId, StringComparison.OrdinalIgnoreCase)) ??
            AudioInputSources.FirstOrDefault(source => source.IsDefault) ??
            AudioInputSources[0];

        AudioInputSelectionStatus = selectedSourceRemoved
            ? "Selected audio input was removed"
            : string.Empty;
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
