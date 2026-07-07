using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Audio;

internal sealed class AudioCaptureStateStore
{
    private readonly Lock _lock = new();
    private AudioCaptureSession? _activeSession;
    private AudioCaptureSettings _idleSettings = AudioCaptureSettings.Default;

    public AudioCaptureStateSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return _activeSession is null
                ? AudioCaptureStateSnapshot.Stopped(_idleSettings)
                : AudioCaptureStateSnapshot.FromSession(_activeSession);
        }
    }

    public void PrepareForAudioCapture(bool defaultDesktopAudioEnabled)
    {
        lock (_lock)
        {
            _idleSettings = _idleSettings.PrepareForCapture(defaultDesktopAudioEnabled);
        }
    }

    public AudioCaptureSession StartSession(string tempAudioPath)
    {
        lock (_lock)
        {
            if (_activeSession is not null)
            {
                throw new InvalidOperationException("Audio capture is already in progress.");
            }

            _activeSession = new AudioCaptureSession(tempAudioPath, _idleSettings);
            return _activeSession;
        }
    }

    public AudioCaptureState PauseOrResume(Guid sessionId)
    {
        lock (_lock)
        {
            AudioCaptureSession session = GetRequiredSession(sessionId);
            return session.PauseOrResume();
        }
    }

    public Guid GetRequiredActiveSessionId()
    {
        lock (_lock)
        {
            if (_activeSession is null)
            {
                throw new InvalidOperationException("Audio capture is not in progress.");
            }

            return _activeSession.Id;
        }
    }

    public void StopSession(Guid sessionId)
    {
        lock (_lock)
        {
            if (_activeSession?.Id != sessionId)
            {
                return;
            }

            _idleSettings = _activeSession.Settings;
            _activeSession = null;
        }
    }

    public AudioCaptureStateSnapshot UpdateSettings(Func<AudioCaptureSettings, AudioCaptureSettings> update)
    {
        lock (_lock)
        {
            if (_activeSession is not null)
            {
                AudioCaptureSettings settings = update(_activeSession.Settings);
                _activeSession.SetSettings(settings);
                _idleSettings = settings;
                return AudioCaptureStateSnapshot.FromSession(_activeSession);
            }

            _idleSettings = update(_idleSettings);
            return AudioCaptureStateSnapshot.Stopped(_idleSettings);
        }
    }

    private AudioCaptureSession GetRequiredSession(Guid sessionId)
    {
        if (_activeSession?.Id != sessionId)
        {
            throw new InvalidOperationException("Audio capture session is no longer active.");
        }

        return _activeSession;
    }
}
