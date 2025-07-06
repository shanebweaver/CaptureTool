using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Themes;
using System;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IAppController _appController;

    public event EventHandler? CaptureRequested;
    public event EventHandler? CloseRequested;

    public RelayCommand RequestCaptureCommand => new(RequestCapture);
    public RelayCommand CloseOverlayCommand => new(CloseOverlay);

    public bool IsPrimary => Monitor?.MonitorBounds.Top == 0 && Monitor.MonitorBounds.Left == 0;
        
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

    private AppTheme _currentAppTheme;
    public AppTheme CurrentAppTheme
    {
        get => _currentAppTheme;
        set => Set(ref _currentAppTheme, value);
    }

    private AppTheme _defaultAppTheme;
    public AppTheme DefaultAppTheme
    {
        get => _defaultAppTheme;
        set => Set(ref _defaultAppTheme, value);
    }

    public CaptureOverlayWindowViewModel(
        IThemeService themeService,
        IAppController appController)
    {
        _appController = appController;
        _captureArea = Rectangle.Empty;

        _themeService = themeService;
        DefaultAppTheme = _themeService.DefaultTheme;
        CurrentAppTheme = _themeService.CurrentTheme;
    }

    public void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseOverlay()
    {
        _appController.CloseCaptureOverlay();
        _appController.ShowMainWindow();
    }

    private void RequestCapture()
    {
        CaptureRequested?.Invoke(this, EventArgs.Empty);
    }
}
