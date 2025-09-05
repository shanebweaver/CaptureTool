using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using System;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayViewModel : LoadableViewModelBase
{
    private readonly IAppController _appController;
    private MonitorCaptureResult? _monitorCaptureResult;
    private Rectangle? _captureArea;

    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        private set => Set(ref _isRecording, value);
    }

    private TimeSpan _captureTime;
    public TimeSpan CaptureTime
    {
        get => _captureTime;
        private set => Set(ref _captureTime, value);
    }

    public RelayCommand CloseOverlayCommand => new(CloseOverlay);
    public RelayCommand GoBackCommand => new(GoBack);
    public RelayCommand StartVideoCaptureCommand => new(StartVideoCapture);
    public RelayCommand StopVideoCaptureCommand => new(StopVideoCapture);

    public CaptureOverlayViewModel(IAppController appController) 
    {
        _appController = appController;
    }

    public override void Load(object? parameter)
    {
        if (parameter is (MonitorCaptureResult monitor, Rectangle area))
        {
            _monitorCaptureResult = monitor;
            _captureArea = area;
        }

        base.Load(parameter);
    }

    public override void Unload()
    {
        base.Unload();
    }

    private void CloseOverlay()
    {
        if (IsRecording)
        {
            _appController.CancelVideoCapture();
        }

        _appController.CloseCaptureOverlay();
        _appController.ShowMainWindow(true);
    }

    private void GoBack()
    {
        if (IsRecording)
        {
            _appController.CancelVideoCapture();
        }

        _appController.CloseCaptureOverlay();
        _appController.ShowSelectionOverlay();
    }

    private void StartVideoCapture()
    {
        if (!IsRecording && _monitorCaptureResult != null && _captureArea != null)
        {
           IsRecording = true;
            _appController.StartVideoCapture(_monitorCaptureResult.Value, _captureArea.Value);
        }
    }

    private void StopVideoCapture()
    {
        if (IsRecording)
        {
            IsRecording = false;
            _appController.StopVideoCapture();
        }
    }
}
