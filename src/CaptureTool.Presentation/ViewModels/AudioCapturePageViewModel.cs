using CaptureTool.Presentation.ViewModels.Helpers;
using CaptureTool.Application.Abstractions.UseCases.AudioCapture;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class AudioCapturePageViewModel : ViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string Start = "Start";
        public static readonly string Stop = "Stop";
        public static readonly string Pause = "Pause";
        public static readonly string Mute = "Mute";
        public static readonly string ToggleDesktopAudio = "ToggleDesktopAudio";
    }

    private const string TelemetryContext = "AudioCapturePage";

    public IAppCommand StartCommand { get; }
    public IAppCommand StopCommand { get; }
    public IAppCommand PauseCommand { get; }
    public IAppCommand MuteCommand { get; }
    public IAppCommand ToggleDesktopAudioCommand { get; }

    public bool CanStartRecording
    {
        get => field;
        private set => Set(ref field, value);
    }

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

    public bool IsMuted
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsDesktopAudioEnabled
    {
        get => field;
        private set => Set(ref field, value);
    }

    private readonly IAudioCaptureHandler _audioCaptureHandler;
    private readonly IAudioCaptureStartUseCase _startAction;
    private readonly IAudioCaptureStopUseCase _stopAction;
    private readonly IAudioCapturePauseUseCase _pauseAction;
    private readonly IAudioCaptureMuteUseCase _muteAction;
    private readonly IAudioCaptureToggleDesktopAudioUseCase _toggleDesktopAudioAction;

    public AudioCapturePageViewModel(
        IAudioCaptureHandler audioCaptureHandler,
        IAudioCaptureStartUseCase startAction,
        IAudioCaptureStopUseCase stopAction,
        IAudioCapturePauseUseCase pauseAction,
        IAudioCaptureMuteUseCase muteAction,
        IAudioCaptureToggleDesktopAudioUseCase toggleDesktopAudioAction,
        ITelemetryService telemetryService)
    {
        _audioCaptureHandler = audioCaptureHandler;
        _startAction = startAction;
        _stopAction = stopAction;
        _pauseAction = pauseAction;
        _muteAction = muteAction;
        _toggleDesktopAudioAction = toggleDesktopAudioAction;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        StartCommand = commandFactory.Create(ActivityIds.Start, Start);
        StopCommand = commandFactory.Create(ActivityIds.Stop, Stop);
        PauseCommand = commandFactory.Create(ActivityIds.Pause, Pause);
        MuteCommand = commandFactory.Create(ActivityIds.Mute, Mute);
        ToggleDesktopAudioCommand = commandFactory.Create(ActivityIds.ToggleDesktopAudio, ToggleDesktopAudio);

        // Subscribe to service events for state synchronization
        _audioCaptureHandler.CaptureStateChanged += OnCaptureStateChanged;
        _audioCaptureHandler.MutedStateChanged += OnMutedStateChanged;
        _audioCaptureHandler.DesktopAudioStateChanged += OnDesktopAudioStateChanged;

        // Initialize state from service
        RefreshAudioCaptureStateProperties();
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
        RefreshAudioCaptureStateProperties();
    }

    private void OnMutedStateChanged(object? sender, bool value)
    {
        IsMuted = value;
    }

    private void OnDesktopAudioStateChanged(object? sender, bool value)
    {
        IsDesktopAudioEnabled = value;
    }

    private void Start()
    {
        _startAction.Execute();
    }

    private void Stop()
    {
        _stopAction.Execute();
    }

    private void Pause()
    {
        _pauseAction.Execute();
    }

    private void Mute()
    {
        _muteAction.Execute();
    }

    private void ToggleDesktopAudio()
    {
        _toggleDesktopAudioAction.Execute();
    }

    public override void Dispose()
    {
        _audioCaptureHandler.CaptureStateChanged -= OnCaptureStateChanged;
        _audioCaptureHandler.MutedStateChanged -= OnMutedStateChanged;
        _audioCaptureHandler.DesktopAudioStateChanged -= OnDesktopAudioStateChanged;

        base.Dispose();
    }
}
