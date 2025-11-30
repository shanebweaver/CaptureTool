using CaptureTool.Domains.Capture.Implementations.Windows;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolVideoCaptureHandler : IVideoCaptureHandler
{
    private readonly IStorageService _storageService;
    
    private string? _tempVideoPath;
    private bool isRecording;

    public event EventHandler<IVideoFile>? NewVideoCaptured;

    public CaptureToolVideoCaptureHandler(IStorageService storageService)
    {
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

        ScreenRecorder.StartRecording(args.Monitor.HMonitor, _tempVideoPath);
    }

    public IVideoFile StopVideoCapture()
    {
        if (!isRecording || string.IsNullOrEmpty(_tempVideoPath))
        {
            throw new InvalidOperationException("Cannot stop, no video is recording.");
        }

        ScreenRecorder.StopRecording();

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

            ScreenRecorder.StopRecording();
        }
        finally
        {
           _tempVideoPath = null;
            isRecording = false;
        }
    }
}
