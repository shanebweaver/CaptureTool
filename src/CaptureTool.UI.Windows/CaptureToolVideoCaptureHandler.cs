using CaptureTool.Core.Navigation;
using CaptureTool.Domains.Capture.Implementations.Windows;
using CaptureTool.Domains.Capture.Interfaces;
using Microsoft.Windows.Storage;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolVideoCaptureHandler : IVideoCaptureHandler
{
    private readonly IAppNavigation _appNavigation;

    private string? _tempVideoPath;
    private bool isRecording;

    public CaptureToolVideoCaptureHandler(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public void StartVideoCapture(NewCaptureArgs args)
    {
        if (isRecording)
        {
            throw new InvalidOperationException("A video is already being recorded.");
        }

        isRecording = true;
        _appNavigation.GoToVideoCapture(args);

        _tempVideoPath = Path.Combine(
            ApplicationData.GetDefault().TemporaryPath,
            $"capture_{Guid.NewGuid()}.mp4"
        );

        ScreenRecorder.StartRecording(args.Monitor.HMonitor, _tempVideoPath);
    }

    public VideoFile StopVideoCapture()
    {
        if (!isRecording || string.IsNullOrEmpty(_tempVideoPath))
        {
            throw new InvalidOperationException("Cannot stop, no video is recording.");
        }

        ScreenRecorder.StopRecording();

        VideoFile videoFile = new(_tempVideoPath);
        _appNavigation.GoToVideoEdit(videoFile);
        _tempVideoPath = null;

        return videoFile;
    }

    public void CancelVideoCapture()
    {
        try
        {
            ScreenRecorder.StopRecording();
        }
        finally
        {
           _tempVideoPath = null;
        }
    }
}
