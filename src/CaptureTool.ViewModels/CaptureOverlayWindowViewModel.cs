using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using System;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayWindowViewModel : ViewModelBase
{
    private readonly IAppController _appController;

    public event EventHandler? CaptureRequested;

    public RelayCommand RequestCaptureCommand => new(RequestCapture);
    public RelayCommand CloseOverlayCommand => new(CloseOverlay);

    public bool IsPrimary => Monitor?.MonitorBounds.Top == 0 && Monitor.MonitorBounds.Left == 0;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    private Rectangle _captureArea;
    public Rectangle CaptureArea
    {
        get => _captureArea;
        set => Set(ref _captureArea, value);
    }

    private MonitorCaptureResult? _monitor;
    public MonitorCaptureResult? Monitor
    {
        get => _monitor;
        set => Set(ref _monitor, value);
    }

    public CaptureOverlayWindowViewModel(
        IAppController appController)
    {
        _appController = appController;
        _captureArea = Rectangle.Empty;
    }

    private void CloseOverlay()
    {
        _appController.CleanupCaptureOverlays();
        _appController.RestoreMainWindow();
    }

    private void RequestCapture()
    {
        CaptureRequested?.Invoke(this, EventArgs.Empty);
    }
}
