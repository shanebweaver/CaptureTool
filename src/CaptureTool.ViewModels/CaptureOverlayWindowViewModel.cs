using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureOverlayWindowViewModel : LoadableViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IAppController _appController;
    private readonly IFeatureManager _featureManager;

    public RelayCommand RequestCaptureCommand => new(RequestCapture);
    public RelayCommand CloseOverlayCommand => new(CloseOverlay);
    public RelayCommand TransitionToVideoModeCommand => new(TransitionToVideoMode);
    public RelayCommand StartVideoCaptureCommand => new(StartVideoCapture);
    public RelayCommand StopVideoCaptureCommand => new(StopVideoCapture);

    public bool IsPrimary => Monitor?.IsPrimary ?? false;

    private ObservableCollection<CaptureType> _supportedCaptureTypes;
    public ObservableCollection<CaptureType> SupportedCaptureTypes
    {
        get => _supportedCaptureTypes;
        private set => Set(ref _supportedCaptureTypes, value);
    }

    private int _selectedCaptureTypeIndex;
    public int SelectedCaptureTypeIndex
    {
        get => _selectedCaptureTypeIndex;
        set
        {
            Set(ref _selectedCaptureTypeIndex, value);
            RaisePropertyChanged(nameof(SelectedCaptureType));
            OnSelectedCaptureTypeIndexChanged();
        }
    }

    private void OnSelectedCaptureTypeIndexChanged()
    {
        switch (SelectedCaptureType)
        {
            case CaptureType.FullScreen:
                if (Monitor != null)
                {
                    CaptureArea = Monitor.Value.MonitorBounds;
                }
                break;

            case CaptureType.Window:
            case CaptureType.Rectangle:
            case CaptureType.Freeform:
            case CaptureType.AllScreens:
            default:
                CaptureArea = Rectangle.Empty;
                break;
        }
    }

    private ObservableCollection<CaptureMode> _supportedCaptureModes;
    public ObservableCollection<CaptureMode> SupportedCaptureModes
    {
        get => _supportedCaptureModes;
        private set => Set(ref _supportedCaptureModes, value);
    }

    private int _selectedCaptureModeIndex;
    public int SelectedCaptureModeIndex
    {
        get => _selectedCaptureModeIndex;
        set
        {
            Set(ref _selectedCaptureModeIndex, value);
            RaisePropertyChanged(nameof(SelectedCaptureMode));
        }
    }

    public CaptureMode? SelectedCaptureMode => SupportedCaptureModes[Math.Min(SelectedCaptureModeIndex, SupportedCaptureModes.Count - 1)];
    public CaptureType? SelectedCaptureType => SupportedCaptureTypes[Math.Min(SelectedCaptureTypeIndex, SupportedCaptureTypes.Count - 1)];

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
        private set => Set(ref _currentAppTheme, value);
    }

    private AppTheme _defaultAppTheme;
    public AppTheme DefaultAppTheme
    {
        get => _defaultAppTheme;
        private set => Set(ref _defaultAppTheme, value);
    }

    private CaptureMode _activeCaptureMode;
    public CaptureMode ActiveCaptureMode
    {
        get => _activeCaptureMode;
        set
        {
            Set(ref _activeCaptureMode, value);
            OnActiveCaptureModeChanged();
            RaisePropertyChanged(nameof(IsActiveCaptureModeImage));
            RaisePropertyChanged(nameof(IsActiveCaptureModeVideo));
        }
    }

    private bool _isDesktopAudioEnabled;
    public bool IsDesktopAudioEnabled
    {
        get => _isDesktopAudioEnabled;
        set => Set(ref _isDesktopAudioEnabled, value);
    }

    private bool _isCapturingVideo;
    public bool IsCapturingVideo
    {
        get => _isCapturingVideo;
        set => Set(ref _isCapturingVideo, value);
    }

    public bool IsActiveCaptureModeImage => _activeCaptureMode == CaptureMode.Image;
    public bool IsActiveCaptureModeVideo => _activeCaptureMode == CaptureMode.Video;

    private bool IsVideoCaptureFeatureEnabled { get; }
    private bool IsFreeformModeFeatureEnabled { get; }

    public CaptureOverlayWindowViewModel(
        IFeatureManager featureManager,
        IThemeService themeService,
        IAppController appController)
    {
        _featureManager = featureManager;
        _appController = appController;
        _captureArea = Rectangle.Empty;
        _monitorWindows = [];

        _themeService = themeService;
        DefaultAppTheme = _themeService.DefaultTheme;
        CurrentAppTheme = _themeService.CurrentTheme;

        IsVideoCaptureFeatureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
        IsFreeformModeFeatureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageCapture_FreeformMode);

        _supportedCaptureModes = [ CaptureMode.Image ];
        if (IsVideoCaptureFeatureEnabled)
        {
            _supportedCaptureModes.Add(CaptureMode.Video);
        }

        _supportedCaptureTypes = [CaptureType.Rectangle];
         _supportedCaptureTypes.Add(CaptureType.Window);
        _supportedCaptureTypes.Add(CaptureType.FullScreen);
        if (IsFreeformModeFeatureEnabled)
        {
            _supportedCaptureTypes.Add(CaptureType.Freeform);
        }
        _supportedCaptureTypes.Add(CaptureType.AllScreens);

        _activeCaptureMode = CaptureMode.Image;
        _isDesktopAudioEnabled = true;
    }

    public override void Load(object? parameter)
    {
        if (parameter is (MonitorCaptureResult monitor, IEnumerable<Rectangle> monitorWindows, CaptureOptions options))
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

        base.Load(parameter);
    }

    public override void Unload()
    {
        _selectedCaptureTypeIndex = default;
        _selectedCaptureModeIndex = default;
        _supportedCaptureTypes.Clear();
        _supportedCaptureModes.Clear();
        _monitor = null;
        _monitorWindows.Clear();
        _captureArea = Rectangle.Empty;
        _isDesktopAudioEnabled = false;
        _isCapturingVideo = false;
    }

    private void OnActiveCaptureModeChanged()
    {
        if (_activeCaptureMode == CaptureMode.Image)
        {
            CaptureArea = Rectangle.Empty;
        }
    }

    private void TransitionToVideoMode()
    {
        Trace.Assert(_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture));
        ActiveCaptureMode = CaptureMode.Video;
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
            if (SelectedCaptureMode == CaptureMode.Image)
            {
                _appController.PerformImageCapture(Monitor.Value, CaptureArea);
            }
            else if (SelectedCaptureMode == CaptureMode.Video)
            {
                TransitionToVideoMode();
            }
        }
    }

    private void StartVideoCapture()
    {
        Trace.Assert(_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture));

        if (IsCapturingVideo)
        {
            return;
        }

        if (Monitor.HasValue)
        {
            _appController.StartVideoCapture(Monitor.Value, CaptureArea);
            IsCapturingVideo = true;
        }
    }

    private void StopVideoCapture()
    {
        if (!IsCapturingVideo)
        {
            return;
        }

        _appController.StopVideoCapture();
        IsCapturingVideo = false;
    }
}