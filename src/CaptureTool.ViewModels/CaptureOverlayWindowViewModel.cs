using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Themes;
using System;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IAppController _appController;

    public event EventHandler? CaptureRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler<CaptureMode>? SelectedCaptureModeChanged;
    public event EventHandler<CaptureType>? SelectedCaptureTypeChanged;

    public RelayCommand RequestCaptureCommand => new(RequestCapture);
    public RelayCommand CloseOverlayCommand => new(CloseOverlay);

    public bool IsPrimary => Monitor?.MonitorBounds.Top == 0 && Monitor?.MonitorBounds.Left == 0;


    private ObservableCollection<CaptureType> _supportedCaptureTypes;
    public ObservableCollection<CaptureType> SupportedCaptureTypes
    {
        get => _supportedCaptureTypes;
        set => Set(ref _supportedCaptureTypes, value);
    }

    private int _selectedCaptureTypeIndex;
    public int SelectedCaptureTypeIndex
    {
        get => _selectedCaptureTypeIndex;
        set
        {
            if (Set(ref _selectedCaptureTypeIndex, value))
            {
                SelectedCaptureTypeChanged?.Invoke(this, SelectedCaptureType);
            }
        }
    }

    private ObservableCollection<CaptureMode> _supportedCaptureModes;
    public ObservableCollection<CaptureMode> SupportedCaptureModes
    {
        get => _supportedCaptureModes;
        set => Set(ref _supportedCaptureModes, value);
    }

    private int _selectedCaptureModeIndex;
    public int SelectedCaptureModeIndex
    {
        get => _selectedCaptureModeIndex;
        set
        {
            if (Set(ref _selectedCaptureModeIndex, value))
            {
                SelectedCaptureModeChanged?.Invoke(this, SelectedCaptureMode);
            }
        }
    }

    public CaptureMode SelectedCaptureMode => SupportedCaptureModes[SelectedCaptureModeIndex];
    public CaptureType SelectedCaptureType => SupportedCaptureTypes[Math.Min(SelectedCaptureTypeIndex, SupportedCaptureTypes.Count - 1)];

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

    public bool IsVideoCaptureEnabled { get; }
    public bool IsWindowModeEnabled { get; }
    public bool IsFullScreenModeEnabled { get; }
    public bool IsFreeformModeEnabled { get; }

    public CaptureOverlayWindowViewModel(
        IFeatureManager featureManager,
        IThemeService themeService,
        IAppController appController)
    {
        _appController = appController;
        _captureArea = Rectangle.Empty;

        _themeService = themeService;
        DefaultAppTheme = _themeService.DefaultTheme;
        CurrentAppTheme = _themeService.CurrentTheme;

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
        IsWindowModeEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageCapture_WindowMode);
        IsFullScreenModeEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageCapture_FullScreenMode);
        IsFreeformModeEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageCapture_FreeformMode);

        _supportedCaptureModes = [ CaptureMode.Image ];
        if (IsVideoCaptureEnabled)
        {
            _supportedCaptureModes.Add(CaptureMode.Video);
        }
        _selectedCaptureModeIndex = 0;

        _supportedCaptureTypes = [CaptureType.Rectangle];
        if (IsWindowModeEnabled)
        {
            _supportedCaptureTypes.Add(CaptureType.Window);
        }
        if (IsFullScreenModeEnabled)
        {
            _supportedCaptureTypes.Add(CaptureType.FullScreen);
        }
        if (IsFreeformModeEnabled)
        {
            _supportedCaptureTypes.Add(CaptureType.Freeform);
        }
        _selectedCaptureTypeIndex = 0;
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
