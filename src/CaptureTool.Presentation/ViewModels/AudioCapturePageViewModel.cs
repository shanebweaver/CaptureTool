using CaptureTool.Application.Abstractions.AudioCapture;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

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
        IStartAudioCaptureAppCommand startAction,
        IStopAudioCaptureAppCommand stopAction,
        IPauseAudioCaptureAppCommand pauseAction,
        IMuteAudioCaptureAppCommand muteAction,
        IToggleLocalAudioCaptureAppCommand toggleDesktopAudioAction)
    {
        _audioCaptureHandler = audioCaptureHandler;

        StartCommand = startAction.ToRelayCommand();
        StopCommand = stopAction.ToRelayCommand();
        PauseCommand = pauseAction.ToRelayCommand();
        MuteCommand = muteAction.ToRelayCommand();
        ToggleDesktopAudioCommand = toggleDesktopAudioAction.ToRelayCommand();

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
