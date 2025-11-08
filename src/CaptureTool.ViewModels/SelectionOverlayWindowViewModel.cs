using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Common.Storage;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

namespace CaptureTool.ViewModels;

public sealed partial class SelectionOverlayWindowViewModel : LoadableViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;
    private readonly IFactoryService<CaptureTypeViewModel, CaptureType> _captureTypeViewModelFactory;

    private static readonly CaptureType[] _imageCaptureTypes = [
        CaptureType.Rectangle,
        CaptureType.Window,
        CaptureType.FullScreen,
        CaptureType.AllScreens,
    ];

    private static readonly CaptureType[] _videoCaptureTypes = [
        CaptureType.FullScreen,
    ];

    public RelayCommand RequestCaptureCommand => new(RequestCapture);
    public RelayCommand CloseOverlayCommand => new(CloseOverlay);

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
                UpdateSupportedCaptureTypes();
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
        INavigationService navigationService,
        IFeatureManager featureManager,
        IThemeService themeService,
        IAppController appController,
        IFactoryService<CaptureModeViewModel, CaptureMode> captureModeViewModelFactory,
        IFactoryService<CaptureTypeViewModel, CaptureType> captureTypeViewModelFactory)
    {
        _navigationService = navigationService;
        _appController = appController;
        _captureTypeViewModelFactory = captureTypeViewModelFactory;
        _captureArea = Rectangle.Empty;
        _monitorWindows = [];

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;

        IsVideoCaptureFeatureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);

        CaptureModeViewModel imageModeVM = captureModeViewModelFactory.Create(CaptureMode.Image);
        _supportedCaptureModes = [imageModeVM];
        if (IsVideoCaptureFeatureEnabled)
        {
            CaptureModeViewModel videoModeVM = captureModeViewModelFactory.Create(CaptureMode.Video);
            _supportedCaptureModes.Add(videoModeVM);
        }

        _supportedCaptureTypes = [];
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

            UpdateSupportedCaptureTypes();

            var targetType = SupportedCaptureTypes.First(vm => vm.CaptureType == options.CaptureType);
            SelectedCaptureTypeIndex = SupportedCaptureTypes.IndexOf(targetType);
        }

        base.Load(parameter);
    }

    public override void Dispose()
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
        base.Dispose();
    }

    private void CloseOverlay()
    {
        _navigationService.GoBackWhile(r => r.Route == CaptureToolNavigationRoutes.ImageCapture || r.Route == CaptureToolNavigationRoutes.VideoCapture);
    }

    private void UpdateSupportedCaptureTypes()
    {
        SupportedCaptureTypes.Clear();
        if (SupportedCaptureModes.Count == 0)
        {
            SelectedCaptureTypeIndex = -1;
            return;
        }

        var supportedCaptureTypes = SelectedCaptureMode.CaptureMode switch
        {
            CaptureMode.Image => _imageCaptureTypes,
            CaptureMode.Video => _videoCaptureTypes,
            _ => []
        };

        foreach (var supportedCaptureType in supportedCaptureTypes)
        {
            SupportedCaptureTypes.Add(_captureTypeViewModelFactory.Create(supportedCaptureType));
        }

        SelectedCaptureTypeIndex = 0;
    }

    private void RequestCapture()
    {
        if (Monitor != null && CaptureArea != Rectangle.Empty)
        {
            if (SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode == CaptureMode.Image)
            {
                NewCaptureArgs args = new(Monitor.Value, CaptureArea);
                ImageFile image = _appController.PerformImageCapture(args);
                _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, image, true);

            }
            else if (SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode == CaptureMode.Video)
            {
                NewCaptureArgs args = new(Monitor.Value, CaptureArea);
                _navigationService.Navigate(CaptureToolNavigationRoutes.VideoCapture, args);
            }
        }
    }
}