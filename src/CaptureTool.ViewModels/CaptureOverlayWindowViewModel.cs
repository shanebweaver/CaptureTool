using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IAppController _appController;

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
                RaisePropertyChanged(nameof(SelectedCaptureType));
                OnSelectedCaptureTypeChanged();
            }
        }
    }

    private void OnSelectedCaptureTypeChanged()
    {
        switch (SelectedCaptureType)
        {
            case CaptureType.Rectangle:
                CaptureArea = Rectangle.Empty;
                break;

            case CaptureType.Window:
                CaptureArea = Rectangle.Empty;
                break;

            case CaptureType.FullScreen:
                if (Monitor != null)
                {
                    CaptureArea = Monitor.Value.MonitorBounds;
                }
                break;

            case CaptureType.Freeform:
                CaptureArea = Rectangle.Empty;
                break;

            case CaptureType.AllScreens:
                _appController.PerformAllScreensCapture();
                break;
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
                RaisePropertyChanged(nameof(SelectedCaptureMode));
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
        private set => Set(ref _monitor, value);
    }

    private IList<Rectangle> _monitorWindows;
    public IList<Rectangle> MonitorWindows
    {
        get => _monitorWindows;
        private set => Set(ref _monitorWindows, value);
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
    public bool IsFreeformModeEnabled { get; }

    public CaptureOverlayWindowViewModel(
        IFeatureManager featureManager,
        IThemeService themeService,
        IAppController appController)
    {
        _appController = appController;
        _captureArea = Rectangle.Empty;
        _monitorWindows = [];

        _themeService = themeService;
        DefaultAppTheme = _themeService.DefaultTheme;
        CurrentAppTheme = _themeService.CurrentTheme;

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
        IsWindowModeEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageCapture_WindowMode);
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
        _supportedCaptureTypes.Add(CaptureType.FullScreen);
        if (IsFreeformModeEnabled)
        {
            _supportedCaptureTypes.Add(CaptureType.Freeform);
        }
        _supportedCaptureTypes.Add(CaptureType.AllScreens);
        _selectedCaptureTypeIndex = 0;
    }

    public void Load(MonitorCaptureResult monitor, IEnumerable<Rectangle> monitorWindows, CaptureOptions options)
    {
        Monitor = monitor;
        MonitorWindows = [.. monitorWindows];

        if (SupportedCaptureModes.Contains(options.CaptureMode))
        {
            SelectedCaptureModeIndex = SupportedCaptureModes.IndexOf(options.CaptureMode);
        }

        if (SupportedCaptureTypes.Contains(options.CaptureType))
        {
            SelectedCaptureTypeIndex = SupportedCaptureTypes.IndexOf(options.CaptureType);
        }
    }

    public void Unload()
    {
        _selectedCaptureTypeIndex = default;
        _selectedCaptureModeIndex = default;
        _supportedCaptureTypes.Clear();
        _supportedCaptureModes.Clear();
        _monitor = null;
        _monitorWindows.Clear();
        _captureArea = Rectangle.Empty;
    }

    private void CloseOverlay()
    {
        _appController.CloseCaptureOverlay();
        _appController.ShowMainWindow();
    }

    private void RequestCapture()
    {
        if (Monitor != null && CaptureArea != Rectangle.Empty)
        {
            _appController.PerformCapture(Monitor.Value, CaptureArea);
        }
    }
}
