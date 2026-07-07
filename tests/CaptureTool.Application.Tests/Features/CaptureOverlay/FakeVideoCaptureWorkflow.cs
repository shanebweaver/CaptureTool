using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Tests.Features.CaptureOverlay;

internal sealed class FakeVideoCaptureWorkflow : IVideoCaptureWorkflow
{
    public event EventHandler<VideoFile>? NewVideoCaptured;
    public event EventHandler? RecordingStarted;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<bool>? PausedStateChanged;

    public bool IsDesktopAudioEnabled { get; set; }
    public bool IsAudioInputMuted { get; set; }
    public int AudioInputVolumePercentage { get; set; } = 100;
    public bool IsRecording { get; set; }
    public bool IsFinalizing { get; set; }
    public bool IsPaused { get; set; }
    public string? SelectedAudioInputSourceId { get; set; }

    public NewCaptureArgs? StartedCaptureArgs { get; private set; }
    public bool PrepareWasCalled { get; private set; }
    public int CancelCallCount { get; private set; }
    public int StopCallCount { get; private set; }
    public bool? LastDesktopAudioEnabled { get; private set; }
    public bool? LastAudioInputMuted { get; private set; }
    public string? LastSelectedAudioInputSourceId { get; private set; }
    public bool SelectAudioInputSourceWasCalled { get; private set; }
    public bool? LastPausedState { get; private set; }
    public bool ThrowOnCancel { get; set; }
    public PendingVideoFile PendingVideo { get; set; } = new("capture.mp4");

    public void PrepareForVideoCapture()
    {
        PrepareWasCalled = true;
    }

    public void StartVideoCapture(NewCaptureArgs args)
    {
        StartedCaptureArgs = args;
        IsRecording = true;
    }

    public PendingVideoFile StopVideoCapture()
    {
        StopCallCount++;
        IsRecording = false;
        IsFinalizing = true;
        NewVideoCaptured?.Invoke(this, PendingVideo);
        return PendingVideo;
    }

    public void CancelVideoCapture()
    {
        CancelCallCount++;
        if (ThrowOnCancel)
        {
            throw new InvalidOperationException("Cancel failed.");
        }

        IsRecording = false;
    }

    public void SetIsDesktopAudioEnabled(bool value)
    {
        IsDesktopAudioEnabled = value;
        LastDesktopAudioEnabled = value;
        DesktopAudioStateChanged?.Invoke(this, value);
    }

    public void SetIsAudioInputMuted(bool value)
    {
        IsAudioInputMuted = value;
        LastAudioInputMuted = value;
    }

    public void SelectAudioInputSource(string? sourceId)
    {
        SelectedAudioInputSourceId = sourceId;
        LastSelectedAudioInputSourceId = sourceId;
        SelectAudioInputSourceWasCalled = true;
    }

    public void SetAudioInputVolume(int volumePercentage)
    {
        AudioInputVolumePercentage = volumePercentage;
    }

    public void ToggleDesktopAudioCapture(bool enabled)
    {
        LastDesktopAudioEnabled = enabled;
    }

    public void ToggleIsPaused(bool isPaused)
    {
        IsPaused = isPaused;
        LastPausedState = isPaused;
        PausedStateChanged?.Invoke(this, isPaused);
    }

    public void RaiseRecordingStarted()
    {
        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }
}
