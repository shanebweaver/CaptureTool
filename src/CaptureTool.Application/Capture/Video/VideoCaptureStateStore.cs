using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Video;

internal sealed class VideoCaptureStateStore
{
    private readonly Lock _lock = new();
    private VideoCaptureSession? _activeSession;
    private VideoCaptureAudioSettings _idleAudioSettings = VideoCaptureAudioSettings.Default;

    public VideoCaptureStateSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return _activeSession is null
                ? VideoCaptureStateSnapshot.Idle(_idleAudioSettings)
                : VideoCaptureStateSnapshot.FromSession(_activeSession);
        }
    }

    public void PrepareForVideoCapture(bool defaultDesktopAudioEnabled)
    {
        lock (_lock)
        {
            _idleAudioSettings = _idleAudioSettings.PrepareForCapture(defaultDesktopAudioEnabled);
        }
    }

    public VideoCaptureSession StartSession(string tempVideoPath, CaptureRecordingTarget target)
    {
        lock (_lock)
        {
            if (_activeSession is not null)
            {
                throw new InvalidOperationException("A video is already being recorded.");
            }

            _activeSession = new VideoCaptureSession(tempVideoPath, target, _idleAudioSettings);
            return _activeSession;
        }
    }

    public VideoCaptureSession GetRequiredActiveSession()
    {
        lock (_lock)
        {
            if (_activeSession is null)
            {
                throw new InvalidOperationException("Video capture is not recording.");
            }

            return _activeSession;
        }
    }

    public VideoCaptureSession? GetCancelableSession()
    {
        lock (_lock)
        {
            return _activeSession?.Status is VideoCaptureStatus.Recording or VideoCaptureStatus.Paused
                ? _activeSession
                : null;
        }
    }

    public VideoCaptureFinalization BeginFinalizing()
    {
        lock (_lock)
        {
            if (_activeSession is null)
            {
                throw new InvalidOperationException("Cannot stop, no video is recording.");
            }

            bool wasPaused = _activeSession.Status == VideoCaptureStatus.Paused;
            PendingVideoFile pendingVideo = _activeSession.BeginFinalizing();
            _idleAudioSettings = _activeSession.AudioSettings;
            return new VideoCaptureFinalization(
                _activeSession.Id,
                pendingVideo,
                wasPaused,
                _activeSession.Target,
                _activeSession.AudioSettings);
        }
    }

    public void ClearSession(Guid sessionId)
    {
        lock (_lock)
        {
            if (_activeSession?.Id != sessionId)
            {
                return;
            }

            _idleAudioSettings = _activeSession.AudioSettings;
            _activeSession = null;
        }
    }

    public bool TryMarkRecordingStarted()
    {
        lock (_lock)
        {
            return _activeSession?.TryMarkRecordingStarted() == true;
        }
    }

    public bool SetPaused(Guid sessionId, bool isPaused)
    {
        lock (_lock)
        {
            VideoCaptureSession session = GetRequiredSession(sessionId);
            return session.SetPaused(isPaused);
        }
    }

    public VideoCaptureStateSnapshot UpdateAudioSettings(Func<VideoCaptureAudioSettings, VideoCaptureAudioSettings> update)
    {
        lock (_lock)
        {
            if (_activeSession is not null)
            {
                VideoCaptureAudioSettings audioSettings = update(_activeSession.AudioSettings);
                _activeSession.SetAudioSettings(audioSettings);
                _idleAudioSettings = audioSettings;
                return VideoCaptureStateSnapshot.FromSession(_activeSession);
            }

            _idleAudioSettings = update(_idleAudioSettings);
            return VideoCaptureStateSnapshot.Idle(_idleAudioSettings);
        }
    }

    private VideoCaptureSession GetRequiredSession(Guid sessionId)
    {
        if (_activeSession?.Id != sessionId)
        {
            throw new InvalidOperationException("Video capture session is no longer active.");
        }

        return _activeSession;
    }
}
