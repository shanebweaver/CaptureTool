using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Navigation;
using System;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayWindowViewModel : ViewModelBase
{
    private readonly IAppController _appController;

    public event EventHandler? CaptureRequested;

    public RelayCommand RequestCaptureCommand => new(RequestCapture);
    public RelayCommand CloseOverlayCommand => new(CloseOverlay);
    public RelayCommand ToggleShowOptionsCommand => new(ToggleShowOptions);

    public bool IsPrimary => Monitor?.MonitorBounds.Top == 0 && Monitor.MonitorBounds.Left == 0;

    private Rectangle _captureArea;
    public Rectangle CaptureArea
    {
        get => _captureArea;
        set => Set(ref _captureArea, value);
    }

    private bool _showOptions;
    public bool ShowOptions
    {
        get => _showOptions;
        set => Set(ref _showOptions, value);
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

        _captureArea = new(0,0,0,0);
    }

    private void CloseOverlay()
    {
        _appController.CloseCaptureOverlays();
    }

    private void ToggleShowOptions()
    {
        ShowOptions = !ShowOptions;
    }

    private void RequestCapture()
    {
        CaptureRequested?.Invoke(this, EventArgs.Empty);
    }
}
