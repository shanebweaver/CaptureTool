using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Core.Capture;

public partial class CaptureToolVideoCaptureHandler : IVideoCaptureHandler
{
    private readonly IScreenRecorder _screenRecorder;
    private readonly IStorageService _storageService;
    
    private string? _tempVideoPath;
    private bool isRecording;

    public event EventHandler<IVideoFile>? NewVideoCaptured;

    public CaptureToolVideoCaptureHandler(
        IScreenRecorder screenRecorder,
        IStorageService storageService)
    {
        _screenRecorder = screenRecorder;
        _storageService = storageService;
    }

    public void StartVideoCapture(NewCaptureArgs args)
    {
        if (isRecording)
        {
            throw new InvalidOperationException("A video is already being recorded.");
        }

        isRecording = true;

        DateTime timestamp = DateTime.Now;
        string fileName = $"Capture {timestamp:yyyy-MM-dd} {timestamp:FFFFF}.mp4";
        _tempVideoPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            fileName
        );

        _screenRecorder.StartRecording(args.Monitor.HMonitor, _tempVideoPath);
    }

    public IVideoFile StopVideoCapture()
    {
        if (!isRecording || string.IsNullOrEmpty(_tempVideoPath))
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
            if (!isRecording)
            {
                return;
            }

            _screenRecorder.StopRecording();
        }
        finally
        {
           _tempVideoPath = null;
            isRecording = false;
        }
    }
}
