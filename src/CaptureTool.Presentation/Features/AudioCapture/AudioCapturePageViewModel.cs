using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CaptureTool.Presentation.Features.AudioCapture;

public sealed partial class AudioCapturePageViewModel : ViewModelBase
{
    public IRelayCommand StartCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IRelayCommand PauseCommand { get; }
    public IRelayCommand MuteCommand { get; }
    public IRelayCommand ToggleDesktopAudioCommand { get; }

    public bool CanStartRecording
    {
        get;
        private set => Set(ref field, value);
    }

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

    public bool IsMuted
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsDesktopAudioEnabled
    {
        get;
        private set => Set(ref field, value);
    }

    public TimeSpan CaptureTime
    {
        get;
        private set => Set(ref field, value);
    }

    private readonly IAudioCaptureHandler _audioCaptureHandler;
    private readonly IAudioInputDetectionService _audioInputDetectionService;
    private readonly ITaskEnvironment _taskEnvironment;
    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
    private const string DefaultAudioInputSuffix = " (Default)";
    private Timer? _timer;
    private DateTime _captureStartTime;
    private TimeSpan _pausedDuration;
    private DateTime? _pauseStartTime;

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

    public AudioCapturePageViewModel(
        IAudioCaptureHandler audioCaptureHandler,
        IAudioInputDetectionService audioInputDetectionService,
        IStartAudioCaptureUseCase startAction,
        IStopAudioCaptureUseCase stopAction,
        IPauseAudioCaptureUseCase pauseAction,
        IMuteAudioCaptureUseCase muteAction,
        IToggleLocalAudioCaptureUseCase toggleDesktopAudioAction,
        ITaskEnvironment taskEnvironment)
    {
        _audioCaptureHandler = audioCaptureHandler;
        _audioInputDetectionService = audioInputDetectionService;
        _taskEnvironment = taskEnvironment;
        SelectedAudioInputSourceIndex = -1;
        AudioInputSources = [];

        StartCommand = startAction.ToRelayCommand(() => new StartAudioCaptureRequest());
        StopCommand = stopAction.ToRelayCommand(() => new StopAudioCaptureRequest());
        PauseCommand = pauseAction.ToRelayCommand(() => new PauseAudioCaptureRequest());
        MuteCommand = muteAction.ToRelayCommand(() => new MuteAudioCaptureRequest());
        ToggleDesktopAudioCommand = toggleDesktopAudioAction.ToRelayCommand(() => new ToggleLocalAudioCaptureRequest());

        // Subscribe to service events for state synchronization
        _audioCaptureHandler.CaptureStateChanged += OnCaptureStateChanged;
        _audioCaptureHandler.RecordingStarted += OnRecordingStarted;
        _audioCaptureHandler.MutedStateChanged += OnMutedStateChanged;
        _audioCaptureHandler.DesktopAudioStateChanged += OnDesktopAudioStateChanged;
        _audioInputDetectionService.AudioInputSourcesChanged += OnAudioInputSourcesChanged;

        // Initialize state from service
        RefreshAudioCaptureStateProperties();
        StartAudioInputDetection();
    }

    private void RefreshAudioCaptureStateProperties()
    {
        CanStartRecording = !_audioCaptureHandler.IsRecording;
        IsRecording = _audioCaptureHandler.IsRecording;
        IsPaused = _audioCaptureHandler.IsPaused;
        IsMuted = _audioCaptureHandler.IsMuted;
        IsDesktopAudioEnabled = _audioCaptureHandler.IsDesktopAudioEnabled;
    }

    private void OnCaptureStateChanged(object? sender, AudioCaptureState value)
    {
        _taskEnvironment.TryExecute(() =>
        {
            ApplyCaptureState(value);
        });
    }

    private void OnRecordingStarted(object? sender, EventArgs e)
    {
        _taskEnvironment.TryExecute(() =>
        {
            if (!IsRecording)
            {
                return;
            }

            StartTimer();
        });
    }

    private void OnMutedStateChanged(object? sender, bool value)
    {
        _taskEnvironment.TryExecute(() =>
        {
            IsMuted = value;
        });
    }

    private void OnDesktopAudioStateChanged(object? sender, bool value)
    {
        _taskEnvironment.TryExecute(() =>
        {
            IsDesktopAudioEnabled = value;
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
            SetAudioInputMuted(true);
        }
    }

    private async Task RefreshAudioInputSourcesAsync()
    {
        IReadOnlyList<AudioInputSource> sources = await _audioInputDetectionService.GetAudioInputSourcesAsync(CancellationToken.None);

        _taskEnvironment.TryExecute(() =>
        {
            UpdateAudioInputSources(sources);
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
            SelectedAudioInputSource = null;
            SelectedAudioInputSourceIndex = -1;
            _audioCaptureHandler.SelectAudioInputSource(null);
            SetAudioInputMuted(true);
            return;
        }

        bool wasSelectedSourceRemoved =
            !string.IsNullOrWhiteSpace(selectedAudioInputSourceId) &&
            AudioInputSources.All(source => source.Id != selectedAudioInputSourceId);

        if (wasSelectedSourceRemoved)
        {
            SetAudioInputMuted(true);
        }

        SelectedAudioInputSource = GetAudioInputSourceToSelect(selectedAudioInputSourceId);
        SelectedAudioInputSourceIndex = AudioInputSources.IndexOf(SelectedAudioInputSource);
        _audioCaptureHandler.SelectAudioInputSource(SelectedAudioInputSource.Id);
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

    private void SetAudioInputMuted(bool isMuted)
    {
        if (IsMuted == isMuted)
        {
            return;
        }

        _audioCaptureHandler.ToggleMute();
    }

    [RelayCommand]
    private void SelectAudioInputSource(AudioInputSource? source)
    {
        if (source == null)
        {
            return;
        }

        bool isAvailable = AudioInputSources.Any(input => string.Equals(input.Id, source.Id, StringComparison.OrdinalIgnoreCase));
        if (isAvailable)
        {
            SelectedAudioInputSource = source;
            SelectedAudioInputSourceIndex = AudioInputSources.IndexOf(source);
            _audioCaptureHandler.SelectAudioInputSource(source.Id);
            return;
        }

        _ = RefreshAudioInputSourcesAsync();
    }

    private void ApplyCaptureState(AudioCaptureState value)
    {
        bool wasRecording = IsRecording;
        bool wasPaused = IsPaused;

        RefreshAudioCaptureStateProperties();

        switch (value)
        {
            case AudioCaptureState.Recording:
                if (wasRecording && wasPaused && _pauseStartTime.HasValue)
                {
                    _pausedDuration += DateTime.UtcNow - _pauseStartTime.Value;
                    _pauseStartTime = null;
                }
                break;

            case AudioCaptureState.Paused:
                if (!wasPaused)
                {
                    _pauseStartTime = DateTime.UtcNow;
                }
                break;

            case AudioCaptureState.Stopped:
                StopTimer();
                CaptureTime = TimeSpan.Zero;
                _pausedDuration = TimeSpan.Zero;
                _pauseStartTime = null;
                break;
        }
    }

    private void StartTimer()
    {
        CaptureTime = TimeSpan.Zero;
        _captureStartTime = DateTime.UtcNow;
        _pausedDuration = TimeSpan.Zero;
        _pauseStartTime = null;

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

    public override void Dispose()
    {
        _audioCaptureHandler.CaptureStateChanged -= OnCaptureStateChanged;
        _audioCaptureHandler.RecordingStarted -= OnRecordingStarted;
        _audioCaptureHandler.MutedStateChanged -= OnMutedStateChanged;
        _audioCaptureHandler.DesktopAudioStateChanged -= OnDesktopAudioStateChanged;
        _audioInputDetectionService.AudioInputSourcesChanged -= OnAudioInputSourcesChanged;

        try
        {
            _audioInputDetectionService.StopWatching();
        }
        catch (Exception)
        {
            // The page can still close if the platform watcher is already gone.
        }

        StopTimer();
        if (_timer != null)
        {
            _timer.Elapsed -= Timer_Elapsed;
            _timer.Dispose();
            _timer = null;
        }

        base.Dispose();
    }
}
