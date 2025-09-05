using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services;
using CaptureTool.Services.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace CaptureTool.ViewModels;

public sealed partial class SelectionOverlayWindowViewModel : LoadableViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IAppController _appController;
    private readonly IFeatureManager _featureManager;

    public RelayCommand RequestCaptureCommand => new(RequestCapture);
    public RelayCommand CloseOverlayCommand => new(CloseOverlay);
    public RelayCommand StartVideoCaptureCommand => new(StartVideoCapture);
    public RelayCommand StopVideoCaptureCommand => new(StopVideoCapture);

    public bool IsPrimary => Monitor?.IsPrimary ?? false;

    private ObservableCollection<CaptureTypeViewModel> _supportedCaptureTypes;
    public ObservableCollection<CaptureTypeViewModel> SupportedCaptureTypes
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
            if (Set(ref _selectedCaptureTypeIndex, value))
            {
                RaisePropertyChanged(nameof(SelectedCaptureType));
            }
        }
    }

    public CaptureTypeViewModel SelectedCaptureType => SupportedCaptureTypes[SelectedCaptureTypeIndex];

    private ObservableCollection<CaptureModeViewModel> _supportedCaptureModes;
    public ObservableCollection<CaptureModeViewModel> SupportedCaptureModes
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
            if (Set(ref _selectedCaptureModeIndex, value))
            {
                RaisePropertyChanged(nameof(SelectedCaptureMode));
            }
        }
    }

    public CaptureModeViewModel SelectedCaptureMode => SupportedCaptureModes[SelectedCaptureModeIndex];

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

    private bool IsVideoCaptureFeatureEnabled { get; }

    public SelectionOverlayWindowViewModel(
        IFeatureManager featureManager,
        IThemeService themeService,
        IAppController appController,
        IFactoryService<CaptureModeViewModel, CaptureMode> captureModeViewModelFactory,
        IFactoryService<CaptureTypeViewModel, CaptureType> captureTypeViewModelFactory)
    {
        _featureManager = featureManager;
        _appController = appController;
        _captureArea = Rectangle.Empty;
        _monitorWindows = [];

        _themeService = themeService;
        DefaultAppTheme = _themeService.DefaultTheme;
        CurrentAppTheme = _themeService.CurrentTheme;

        IsVideoCaptureFeatureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);

        CaptureModeViewModel imageModeVM = captureModeViewModelFactory.Create(CaptureMode.Image);
        _supportedCaptureModes = [imageModeVM];
        if (IsVideoCaptureFeatureEnabled)
        {
            CaptureModeViewModel videoModeVM = captureModeViewModelFactory.Create(CaptureMode.Video);
            _supportedCaptureModes.Add(videoModeVM);
        }

        _supportedCaptureTypes = [
            captureTypeViewModelFactory.Create(CaptureType.Rectangle),
            captureTypeViewModelFactory.Create(CaptureType.Window),
            captureTypeViewModelFactory.Create(CaptureType.FullScreen),
            captureTypeViewModelFactory.Create(CaptureType.AllScreens),
        ];

        _isDesktopAudioEnabled = true;
    }

    public override void Load(object? parameter)
    {
        if (parameter is (MonitorCaptureResult monitor, IEnumerable<Rectangle> monitorWindows, CaptureOptions options))
        {
            Monitor = monitor;
            MonitorWindows = [.. monitorWindows];

            var targetMode = SupportedCaptureModes.First(vm => vm.CaptureMode == options.CaptureMode);
            SelectedCaptureModeIndex = SupportedCaptureModes.IndexOf(targetMode);

            var targetType = SupportedCaptureTypes.First(vm => vm.CaptureType == options.CaptureType);
            SelectedCaptureTypeIndex = SupportedCaptureTypes.IndexOf(targetType);
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

    private void CloseOverlay()
    {
        _appController.CloseSelectionOverlay();
        _appController.ShowMainWindow();
    }

    private void RequestCapture()
    {
        if (Monitor != null && CaptureArea != Rectangle.Empty)
        {
            if (SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode == CaptureMode.Image)
            {
                _appController.PerformImageCapture(Monitor.Value, CaptureArea);
            }
            else if (SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode == CaptureMode.Video)
            {
                _appController.PrepareForVideoCapture(Monitor.Value, CaptureArea);
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