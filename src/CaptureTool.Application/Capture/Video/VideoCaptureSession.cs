using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Video;

internal sealed class VideoCaptureSession
{
    private int _hasObservedRecordingStart;

    public Guid Id { get; } = Guid.NewGuid();
    public CaptureRecordingTarget Target { get; }
    public string TempVideoPath { get; }
    public VideoCaptureStatus Status { get; private set; } = VideoCaptureStatus.Recording;
    public VideoCaptureAudioSettings AudioSettings { get; private set; }
    public PendingVideoFile? PendingVideo { get; private set; }

    public VideoCaptureSession(
        string tempVideoPath,
        CaptureRecordingTarget target,
        VideoCaptureAudioSettings audioSettings)
    {
        TempVideoPath = tempVideoPath;
        Target = target;
        AudioSettings = audioSettings;
    }

    public CaptureRecordingOptions CreateRecordingOptions()
        => new(
            Target,
            TempVideoPath,
            AudioSettings.ShouldCaptureDesktopAudio,
            AudioInputSourceId: AudioSettings.ActiveAudioInputSourceId,
            AudioInputVolumePercentage: AudioSettings.AudioInputVolumePercentage);

    public PendingVideoFile BeginFinalizing()
    {
        if (Status is not (VideoCaptureStatus.Recording or VideoCaptureStatus.Paused))
        {
            throw new InvalidOperationException("Cannot stop, no video is recording.");
        }

        PendingVideo = new PendingVideoFile(TempVideoPath);
        Status = VideoCaptureStatus.Finalizing;
        return PendingVideo;
    }

    public bool TryMarkRecordingStarted()
    {
        if (Status is not (VideoCaptureStatus.Recording or VideoCaptureStatus.Paused))
        {
            return false;
        }

        return Interlocked.Exchange(ref _hasObservedRecordingStart, 1) == 0;
    }

    public bool SetPaused(bool isPaused)
    {
        if (isPaused)
        {
            if (Status == VideoCaptureStatus.Paused)
            {
                return false;
            }

            if (Status != VideoCaptureStatus.Recording)
            {
                throw new InvalidOperationException("Video capture is not recording.");
            }

            Status = VideoCaptureStatus.Paused;
            return true;
        }

        if (Status == VideoCaptureStatus.Recording)
        {
            return false;
        }

        if (Status != VideoCaptureStatus.Paused)
        {
            throw new InvalidOperationException("Video capture is not paused.");
        }

        Status = VideoCaptureStatus.Recording;
        return true;
    }

    public void SetAudioSettings(VideoCaptureAudioSettings audioSettings)
    {
        AudioSettings = audioSettings;
    }
}
