using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

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

    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public AudioCapturePageViewModel(
        IAudioCaptureHandler audioCaptureHandler,
        IStartAudioCaptureUseCase startAction,
        IStopAudioCaptureUseCase stopAction,
        IPauseAudioCaptureUseCase pauseAction,
        IMuteAudioCaptureUseCase muteAction,
        IToggleLocalAudioCaptureUseCase toggleDesktopAudioAction,
        ITelemetryService telemetryService)
    {
        _audioCaptureHandler = audioCaptureHandler;

        StartCommand = startAction.ToRelayCommand(() => new StartAudioCaptureRequest(), telemetryService);
        StopCommand = stopAction.ToRelayCommand(() => new StopAudioCaptureRequest(), telemetryService);
        PauseCommand = pauseAction.ToRelayCommand(() => new PauseAudioCaptureRequest(), telemetryService);
        MuteCommand = muteAction.ToRelayCommand(() => new MuteAudioCaptureRequest(), telemetryService);
        ToggleDesktopAudioCommand = toggleDesktopAudioAction.ToRelayCommand(() => new ToggleLocalAudioCaptureRequest(), telemetryService);

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

    public override void Dispose()
    {
        _audioCaptureHandler.CaptureStateChanged -= OnCaptureStateChanged;
        _audioCaptureHandler.MutedStateChanged -= OnMutedStateChanged;
        _audioCaptureHandler.DesktopAudioStateChanged -= OnDesktopAudioStateChanged;

        base.Dispose();
    }
}
