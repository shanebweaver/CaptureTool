using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.SelectAudioCaptureInputSource;
using CaptureTool.Application.Abstractions.Capture.Audio.StartAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.StopAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.Audio;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CaptureTool.Presentation.Features.AudioCapture;

public sealed partial class AudioCapturePageViewModel : ViewModelBase
{
    private const int WaveformBarCount = 128;
    private const double WaveformMinBarHeight = 0;
    private const double WaveformMaxBarHeight = 132;
    private static readonly TimeSpan WaveformUpdateInterval = TimeSpan.FromMilliseconds(50);
    public IRelayCommand StartCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IRelayCommand CancelCommand { get; }
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

    private readonly IAudioCaptureState _audioCaptureState;
    private readonly IAudioInputDetectionService _audioInputDetectionService;
    private readonly IMuteAudioCaptureUseCase _muteCommand;
    private readonly ISelectAudioCaptureInputSourceUseCase _selectAudioInputSourceCommand;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly IAudioWaveformHistory _waveformHistory;
    private readonly List<double> _capturedWaveformLevels = [];
    private readonly object _capturedWaveformLevelsSyncRoot = new();
    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
    private const string DefaultAudioInputSuffix = " (Default)";
    private Timer? _timer;
    private DateTime _captureStartTime;
    private TimeSpan _pausedDuration;
    private DateTime? _pauseStartTime;
    private DateTime _lastWaveformUpdateUtc = DateTime.MinValue;

    public ObservableCollection<AudioInputSource> AudioInputSources { get; }
    public ObservableCollection<AudioWaveformBarViewModel> WaveformBars { get; }

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
        IAudioCaptureState audioCaptureState,
        IAudioInputDetectionService audioInputDetectionService,
        IStartAudioCaptureUseCase startAction,
        IStopAudioCaptureUseCase stopAction,
        ICancelAudioCaptureUseCase cancelAction,
        IPauseAudioCaptureUseCase pauseAction,
        IMuteAudioCaptureUseCase muteAction,
        ISelectAudioCaptureInputSourceUseCase selectAudioInputSourceAction,
        IToggleLocalAudioCaptureUseCase toggleDesktopAudioAction,
        ITaskEnvironment taskEnvironment,
        IAudioWaveformHistory waveformHistory)
    {
        _audioCaptureState = audioCaptureState;
        _audioInputDetectionService = audioInputDetectionService;
        _muteCommand = muteAction;
        _selectAudioInputSourceCommand = selectAudioInputSourceAction;
        _taskEnvironment = taskEnvironment;
        _waveformHistory = waveformHistory;
        SelectedAudioInputSourceIndex = -1;
        AudioInputSources = [];
        WaveformBars = [];
        ClearWaveform();

        StartCommand = startAction.ToRelayCommand(() => new StartAudioCaptureRequest());
        StopCommand = stopAction.ToRelayCommand(() => new StopAudioCaptureRequest());
        CancelCommand = cancelAction.ToRelayCommand(() => new CancelAudioCaptureRequest());
        PauseCommand = pauseAction.ToRelayCommand(() => new PauseAudioCaptureRequest());
        MuteCommand = muteAction.ToRelayCommand(() => new MuteAudioCaptureRequest());
        ToggleDesktopAudioCommand = toggleDesktopAudioAction.ToRelayCommand(() => new ToggleLocalAudioCaptureRequest());

        // Subscribe to service events for state synchronization
        _audioCaptureState.CaptureStateChanged += OnCaptureStateChanged;
        _audioCaptureState.MutedStateChanged += OnMutedStateChanged;
        _audioCaptureState.DesktopAudioStateChanged += OnDesktopAudioStateChanged;
        _audioCaptureState.AudioInputSourceChanged += OnAudioInputSourceChanged;
        _audioCaptureState.NewAudioCaptured += OnNewAudioCaptured;
        _audioCaptureState.AudioLevelCaptured += OnAudioLevelCaptured;
        _audioInputDetectionService.AudioInputSourcesChanged += OnAudioInputSourcesChanged;

        // Initialize state from service
        RefreshAudioCaptureStateProperties();
        StartAudioInputDetection();
    }

    private void RefreshAudioCaptureStateProperties()
    {
        CanStartRecording = !_audioCaptureState.IsRecording;
        IsRecording = _audioCaptureState.IsRecording;
        IsPaused = _audioCaptureState.IsPaused;
        IsMuted = _audioCaptureState.IsMuted;
        IsDesktopAudioEnabled = _audioCaptureState.IsDesktopAudioEnabled;
    }

    private void OnCaptureStateChanged(object? sender, AudioCaptureState value)
    {
        _taskEnvironment.TryExecute(() =>
        {
            ApplyCaptureState(value);
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

    private void OnAudioInputSourceChanged(object? sender, string? sourceId)
    {
        _taskEnvironment.TryExecute(() => ApplySelectedAudioInputSource(sourceId));
    }

    private void OnAudioLevelCaptured(object? sender, AudioCaptureLevel value)
    {
        DateTime now = DateTime.UtcNow;
        if (now - _lastWaveformUpdateUtc < WaveformUpdateInterval)
        {
            return;
        }

        _lastWaveformUpdateUtc = now;
        double peakLevel = Math.Clamp(value.PeakLevel, 0, 1);
        lock (_capturedWaveformLevelsSyncRoot)
        {
            _capturedWaveformLevels.Add(peakLevel);
        }

        _taskEnvironment.TryExecute(() =>
        {
            AddWaveformLevel(peakLevel);
        });
    }

    private void OnNewAudioCaptured(object? sender, AudioFile audioFile)
    {
        double[] capturedLevels;
        lock (_capturedWaveformLevelsSyncRoot)
        {
            capturedLevels = _capturedWaveformLevels.ToArray();
            _capturedWaveformLevels.Clear();
        }

        _waveformHistory.Save(audioFile.FilePath, capturedLevels);
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
            ApplySelectedAudioInputSource(null);
            _ = RequestAudioInputSourceSelectionAsync(null);
            return;
        }

        ApplySelectedAudioInputSource(_audioCaptureState.SelectedAudioInputSourceId);
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

    private void SetAudioInputMuted(bool isMuted)
    {
        if (IsMuted == isMuted)
        {
            return;
        }

        _ = _muteCommand.ExecuteAsync(new MuteAudioCaptureRequest(), CancellationToken.None);
    }

    private async Task RequestAudioInputSourceSelectionAsync(string? sourceId)
    {
        await _selectAudioInputSourceCommand.ExecuteAsync(
            new SelectAudioCaptureInputSourceRequest(sourceId),
            CancellationToken.None);
    }

    [RelayCommand]
    private async Task SelectAudioInputSourceAsync(AudioInputSource? source)
    {
        if (source == null)
        {
            return;
        }

        bool isAvailable = AudioInputSources.Any(input => string.Equals(input.Id, source.Id, StringComparison.OrdinalIgnoreCase));
        if (isAvailable)
        {
            await RequestAudioInputSourceSelectionAsync(source.Id);
            return;
        }

        await RefreshAudioInputSourcesAsync();
    }

    private void ApplySelectedAudioInputSource(string? sourceId)
    {
        SelectedAudioInputSource = AudioInputSources.FirstOrDefault(source =>
            string.Equals(source.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        SelectedAudioInputSourceIndex = SelectedAudioInputSource is null
            ? -1
            : AudioInputSources.IndexOf(SelectedAudioInputSource);
    }

    private void ApplyCaptureState(AudioCaptureState value)
    {
        bool wasRecording = IsRecording;
        bool wasPaused = IsPaused;

        RefreshAudioCaptureStateProperties();

        switch (value)
        {
            case AudioCaptureState.Recording:
                if (!wasRecording)
                {
                    lock (_capturedWaveformLevelsSyncRoot)
                    {
                        _capturedWaveformLevels.Clear();
                    }

                    ClearWaveform();
                    StartTimer();
                }
                else if (wasPaused && _pauseStartTime.HasValue)
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
                ClearWaveform();
                break;
        }
    }

    internal void AddWaveformLevel(double peakLevel)
    {
        double clampedLevel = Math.Clamp(peakLevel, 0, 1);

        if (WaveformBars.Count >= WaveformBarCount)
        {
            for (int index = 1; index < WaveformBars.Count; index++)
            {
                SetWaveformBar(WaveformBars[index - 1], WaveformBars[index].Level);
            }

            SetWaveformBar(WaveformBars[^1], clampedLevel);
            return;
        }

        WaveformBars.Add(CreateWaveformBar(clampedLevel));
    }

    private static AudioWaveformBarViewModel CreateWaveformBar(double level)
    {
        double clampedLevel = Math.Clamp(level, 0, 1);
        return new AudioWaveformBarViewModel(GetWaveformBarHeight(clampedLevel), level: clampedLevel);
    }

    private static void SetWaveformBar(AudioWaveformBarViewModel bar, double level)
    {
        double clampedLevel = Math.Clamp(level, 0, 1);
        bar.Level = clampedLevel;
        bar.Height = GetWaveformBarHeight(clampedLevel);
    }

    private static double GetWaveformBarHeight(double level)
    {
        double clampedLevel = Math.Clamp(level, 0, 1);
        return WaveformMinBarHeight + (clampedLevel * (WaveformMaxBarHeight - WaveformMinBarHeight));
    }

    private void ClearWaveform()
    {
        WaveformBars.Clear();
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
        _audioCaptureState.CaptureStateChanged -= OnCaptureStateChanged;
        _audioCaptureState.MutedStateChanged -= OnMutedStateChanged;
        _audioCaptureState.DesktopAudioStateChanged -= OnDesktopAudioStateChanged;
        _audioCaptureState.AudioInputSourceChanged -= OnAudioInputSourceChanged;
        _audioCaptureState.NewAudioCaptured -= OnNewAudioCaptured;
        _audioCaptureState.AudioLevelCaptured -= OnAudioLevelCaptured;
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
