using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Core.Implementations.Capture;

public partial class CaptureToolVideoCaptureHandler : IVideoCaptureHandler
{
    private readonly IScreenRecorder _screenRecorder;
    private readonly IStorageService _storageService;
    
    private string? _tempVideoPath;

    public bool IsDesktopAudioEnabled { get; private set; }
    public bool IsMicrophoneEnabled { get; private set; }
    public bool IsRecording { get; private set; }
    public bool IsPaused { get; private set; }

    public event EventHandler<IVideoFile>? NewVideoCaptured;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<bool>? PausedStateChanged;

    public CaptureToolVideoCaptureHandler(
        IScreenRecorder screenRecorder,
        IStorageService storageService)
    {
        _screenRecorder = screenRecorder;
        _storageService = storageService;

        IsDesktopAudioEnabled = true;
        IsMicrophoneEnabled = false;
    }

    public void StartVideoCapture(NewCaptureArgs args)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("A video is already being recorded.");
        }

        IsRecording = true;

        DateTime timestamp = DateTime.Now;
        string fileName = $"Capture {timestamp:yyyy-MM-dd} {timestamp:FFFFF}.mp4";
        _tempVideoPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            fileName
        );

        // Use new 4-parameter method if microphone is enabled, otherwise use legacy 3-parameter method
        if (IsMicrophoneEnabled)
        {
            _screenRecorder.StartRecording(args.Monitor.HMonitor, _tempVideoPath, IsDesktopAudioEnabled, IsMicrophoneEnabled);
        }
        else
        {
            _screenRecorder.StartRecording(args.Monitor.HMonitor, _tempVideoPath, IsDesktopAudioEnabled);
        }
    }

    public IVideoFile StopVideoCapture()
    {
        if (!IsRecording || string.IsNullOrEmpty(_tempVideoPath))
        {
            throw new InvalidOperationException("Cannot stop, no video is recording.");
        }

        _screenRecorder.StopRecording();

        VideoFile videoFile = new(_tempVideoPath);
        _tempVideoPath = null;

        NewVideoCaptured?.Invoke(this, videoFile);
        return videoFile;
    }

    public void CancelVideoCapture()
    {
        try
        {
            if (!IsRecording)
            {
                return;
            }

            _screenRecorder.StopRecording();
        }
        finally
        {
           _tempVideoPath = null;
            IsRecording = false;
        }
    }

    public void SetIsDesktopAudioEnabled(bool value)
    {
        IsDesktopAudioEnabled = value;
        DesktopAudioStateChanged?.Invoke(this, value);
    }

    public void SetIsMicrophoneEnabled(bool value)
    {
        IsMicrophoneEnabled = value;
        // Note: Microphone can only be enabled/disabled before starting recording
        // Runtime toggle not supported yet (Phase 3 will add this)
    }

    public void ToggleDesktopAudioCapture(bool enabled)
    {
        if (IsRecording)
        {
            _screenRecorder.ToggleAudioCapture(enabled);
        }
    }

    public void ToggleIsPaused(bool isPaused)
    {
        IsPaused = isPaused;
        PausedStateChanged?.Invoke(this, isPaused);

        if (IsRecording)
        {
            if (isPaused)
            {
                _screenRecorder.PauseRecording();
            }
            else
            {
                _screenRecorder.ResumeRecording();
            }
        }
    }
}
