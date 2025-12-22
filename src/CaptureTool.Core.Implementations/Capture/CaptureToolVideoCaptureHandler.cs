using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Core.Implementations.Capture;

public partial class CaptureToolVideoCaptureHandler : IVideoCaptureHandler
{
    private readonly IScreenRecorder _screenRecorder;
    private readonly IStorageService _storageService;
    private readonly ILogService _logService;
    
    private string? _tempVideoPath;
    private AudioSampleCallback? _audioSampleCallback;
    private VideoFrameCallback? _videoFrameCallback;

    public bool IsDesktopAudioEnabled { get; private set; }
    public bool IsRecording { get; private set; }
    public bool IsPaused { get; private set; }

    public event EventHandler<IVideoFile>? NewVideoCaptured;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<bool>? PausedStateChanged;

    public CaptureToolVideoCaptureHandler(
        IScreenRecorder screenRecorder,
        IStorageService storageService,
        ILogService logService)
    {
        _screenRecorder = screenRecorder;
        _storageService = storageService;
        _logService = logService;

        IsDesktopAudioEnabled = true;
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

        _screenRecorder.StartRecording(args.Monitor.HMonitor, _tempVideoPath, IsDesktopAudioEnabled);

        _audioSampleCallback = OnAudioSampleCallback;
        _screenRecorder.SetAudioSampleCallback(_audioSampleCallback);

        _videoFrameCallback = OnVideoFrameCallback;
        _screenRecorder.SetVideoFrameCallback(_videoFrameCallback);
    }

    private void OnVideoFrameCallback(ref VideoFrameData frameData)
    {
        _logService.LogInformation($"VIDEO FRAME: {frameData.Timestamp}");
    }

    private void OnAudioSampleCallback(ref AudioSampleData sampleData)
    {
        _logService.LogInformation($"AUDIO SAMPLE: {sampleData.Timestamp}");
    }

    public PendingVideoFile StopVideoCapture()
    {
        if (!IsRecording || string.IsNullOrEmpty(_tempVideoPath))
        {
            throw new InvalidOperationException("Cannot stop, no video is recording.");
        }

        IsRecording = false;
        IsPaused = false;
        string filePath = _tempVideoPath;
        _tempVideoPath = null;

        var pendingVideo = new PendingVideoFile(filePath);

        // Finalize video on a background thread to avoid blocking the UI
        Task.Run(() =>
        {
            try
            {
                _screenRecorder.SetAudioSampleCallback(null);
                _screenRecorder.SetVideoFrameCallback(null);
                _screenRecorder.StopRecording();
                
                var videoFile = new VideoFile(filePath);
                pendingVideo.Complete(videoFile);
                
                NewVideoCaptured?.Invoke(this, videoFile);
            }
            catch (Exception ex)
            {
                pendingVideo.Fail(ex);
                throw;
            }
            finally
            {
                _audioSampleCallback = null;
                _videoFrameCallback = null;
            }
        });

        return pendingVideo;
    }

    public void CancelVideoCapture()
    {
        try
        {
            if (!IsRecording)
            {
                return;
            }

            _screenRecorder.SetAudioSampleCallback(null);
            _screenRecorder.SetVideoFrameCallback(null);
            _screenRecorder.StopRecording();
        }
        finally
        {
            _audioSampleCallback = null;
            _videoFrameCallback = null;
            _tempVideoPath = null;
            IsRecording = false;
            IsPaused = false;
        }
    }

    public void SetIsDesktopAudioEnabled(bool value)
    {
        IsDesktopAudioEnabled = value;
        DesktopAudioStateChanged?.Invoke(this, value);
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
