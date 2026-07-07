using CaptureTool.Application.Features.AudioCapture;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Tests.Features;

internal sealed class FakeAudioCaptureWorkflow : IAudioCaptureWorkflow
{
    public event EventHandler<AudioCaptureState>? CaptureStateChanged;
    public event EventHandler<bool>? MutedStateChanged;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<AudioFile>? NewAudioCaptured;

    public bool IsRecording { get; set; }
    public bool IsPaused { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDesktopAudioEnabled { get; set; } = true;
    public string? SelectedAudioInputSourceId { get; set; }
    public AudioCaptureState CaptureState { get; set; }

    public int StartCallCount { get; private set; }
    public int StopCallCount { get; private set; }
    public int PauseCallCount { get; private set; }
    public int ToggleMuteCallCount { get; private set; }
    public int ToggleLocalAudioCallCount { get; private set; }
    public int SelectAudioInputSourceCallCount { get; private set; }
    public AudioFile AudioFile { get; set; } = new("capture.wav");

    public void StartCapture()
    {
        StartCallCount++;
        IsRecording = true;
        CaptureState = AudioCaptureState.Recording;
        CaptureStateChanged?.Invoke(this, CaptureState);
    }

    public AudioFile StopCapture()
    {
        StopCallCount++;
        IsRecording = false;
        IsPaused = false;
        CaptureState = AudioCaptureState.Stopped;
        CaptureStateChanged?.Invoke(this, CaptureState);
        NewAudioCaptured?.Invoke(this, AudioFile);
        return AudioFile;
    }

    public void PauseCapture()
    {
        PauseCallCount++;
        IsPaused = !IsPaused;
        CaptureState = IsPaused ? AudioCaptureState.Paused : AudioCaptureState.Recording;
        CaptureStateChanged?.Invoke(this, CaptureState);
    }

    public void SelectAudioInputSource(string? sourceId)
    {
        SelectAudioInputSourceCallCount++;
        SelectedAudioInputSourceId = sourceId;
    }

    public void ToggleLocalAudio()
    {
        ToggleLocalAudioCallCount++;
        IsDesktopAudioEnabled = !IsDesktopAudioEnabled;
        DesktopAudioStateChanged?.Invoke(this, IsDesktopAudioEnabled);
    }

    public void ToggleMute()
    {
        ToggleMuteCallCount++;
        IsMuted = !IsMuted;
        MutedStateChanged?.Invoke(this, IsMuted);
    }
}
