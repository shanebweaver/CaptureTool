using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Telemetry;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Themes;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class SelectionOverlayWindowViewModel : LoadableViewModelBase<SelectionOverlayWindowOptions>
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadSelectionOverlayWindow";
        public static readonly string RequestCapture = "RequestCapture";
        public static readonly string CloseOverlay = "CloseOverlay";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly IAppNavigation _appNavigation;
    private readonly IAppController _appController;
    private readonly IImageCaptureHandler _imageCaptureHandler;
    private readonly IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType> _captureTypeViewModelFactory;

    private static readonly CaptureType[] _imageCaptureTypes = [
        CaptureType.Rectangle,
        CaptureType.Window,
        CaptureType.FullScreen,
        CaptureType.AllScreens,
    ];

    private static readonly CaptureType[] _videoCaptureTypes = [
        CaptureType.FullScreen,
    ];

    public RelayCommand RequestCaptureCommand { get; }
    public RelayCommand CloseOverlayCommand { get; }
    public RelayCommand<int> UpdateSelectedCaptureModeCommand { get; }
    public RelayCommand<int> UpdateSelectedCaptureTypeCommand { get; }
    public RelayCommand<Rectangle> UpdateCaptureAreaCommand { get; }

    public bool IsPrimary => Monitor?.IsPrimary ?? false;

    public ObservableCollection<CaptureTypeViewModel> SupportedCaptureTypes
    {
        get => field;
        private set => Set(ref field, value);
    }

    public int SelectedCaptureTypeIndex
    {
        get => field;
        private set => Set(ref field, value);
    }

    public CaptureType? GetSelectedCaptureType()
        => SelectedCaptureTypeIndex != -1 ? SupportedCaptureTypes[SelectedCaptureTypeIndex].CaptureType : null;

    public ObservableCollection<CaptureModeViewModel> SupportedCaptureModes
    {
        get => field;
        private set => Set(ref field, value);
    }

    public int SelectedCaptureModeIndex
    {
        get => field;
        private set => Set(ref field, value);
    }

    public CaptureModeViewModel SelectedCaptureMode => SupportedCaptureModes[SelectedCaptureModeIndex];

    public Rectangle CaptureArea
    {
        get => field;
        private set => Set(ref field, value);
    }

    public MonitorCaptureResult? Monitor
    {
        get => field;
        private set => Set(ref field, value);
    }

    public IList<Rectangle> MonitorWindows
    {
        get => field;
        private set => Set(ref field, value);
    }

    public AppTheme CurrentAppTheme
    {
        get => field;
        private set => Set(ref field, value);
    }

    public AppTheme DefaultAppTheme
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsDesktopAudioEnabled
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsCapturingVideo
    {
        get => field;
        private set => Set(ref field, value);
    }

    private bool IsVideoCaptureFeatureEnabled { get; }

    public SelectionOverlayWindowViewModel(
        ITelemetryService telemetryService,
        IAppNavigation appNavigation,
        IFeatureManager featureManager,
        IThemeService themeService,
        IAppController appController,
        IImageCaptureHandler imageCaptureHandler,
        IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode> captureModeViewModelFactory,
        IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType> captureTypeViewModelFactory)
    {
        _telemetryService = telemetryService;
        _appNavigation = appNavigation;
        _appController = appController;
        _imageCaptureHandler = imageCaptureHandler;
        _captureTypeViewModelFactory = captureTypeViewModelFactory;
        
        CaptureArea = Rectangle.Empty;
        MonitorWindows = [];

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;

        RequestCaptureCommand = new(RequestCapture);
        CloseOverlayCommand = new(CloseOverlay);
        UpdateSelectedCaptureModeCommand = new(UpdateSelectedCaptureMode);
        UpdateSelectedCaptureTypeCommand = new(UpdateSelectedCaptureType);
        UpdateCaptureAreaCommand = new(UpdateCaptureArea);

        IsVideoCaptureFeatureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);

        CaptureModeViewModel imageModeVM = captureModeViewModelFactory.Create(CaptureMode.Image);
        SupportedCaptureModes = [imageModeVM];
        if (IsVideoCaptureFeatureEnabled)
        {
            CaptureModeViewModel videoModeVM = captureModeViewModelFactory.Create(CaptureMode.Video);
            SupportedCaptureModes.Add(videoModeVM);
        }

        SupportedCaptureTypes = [];
        IsDesktopAudioEnabled = true;
    }

    public override void Load(SelectionOverlayWindowOptions options)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Load, () =>
        {
            Monitor = options.Monitor;
            MonitorWindows = [.. options.MonitorWindows];

            var targetMode = SupportedCaptureModes.First(vm => vm.CaptureMode == options.CaptureOptions.CaptureMode);
            UpdateSelectedCaptureMode(SupportedCaptureModes.IndexOf(targetMode));

            UpdateSupportedCaptureTypes();

            var targetType = SupportedCaptureTypes.First(vm => vm.CaptureType == options.CaptureOptions.CaptureType);
            UpdateSelectedCaptureType(SupportedCaptureTypes.IndexOf(targetType));

            base.Load(options);
        });
    }

    private void CloseOverlay()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.CloseOverlay, () =>
        {
            if (_appNavigation.CanGoBack)
            {
                _appNavigation.GoBackToMainWindow();
            }
            else
            {
                _appController.Shutdown();
            }
        });
    }

    private void UpdateCaptureArea(Rectangle area)
    {
        CaptureArea = area;
    }

    private void UpdateSelectedCaptureMode(int index)
    {
        SelectedCaptureModeIndex = index;
        RaisePropertyChanged(nameof(SelectedCaptureMode));
        UpdateSupportedCaptureTypes();
    }

    private void UpdateSelectedCaptureType(int index)
    {
        SelectedCaptureTypeIndex = index;
    }

    private void UpdateSupportedCaptureTypes()
    {
        SupportedCaptureTypes.Clear();
        if (SupportedCaptureModes.Count == 0)
        {
            UpdateSelectedCaptureType(-1);
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

        UpdateSelectedCaptureType(0);
    }

    private void RequestCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.RequestCapture, () =>
        {
            if (Monitor != null && CaptureArea != Rectangle.Empty)
            {
                if (SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode == CaptureMode.Image)
                {
                    NewCaptureArgs args = new(Monitor.Value, CaptureArea);
                    ImageFile image = _imageCaptureHandler.PerformImageCapture(args);
                    _appNavigation.GoToImageEdit(image);

                }
                else if (SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode == CaptureMode.Video)
                {
                    NewCaptureArgs args = new(Monitor.Value, CaptureArea);
                    _appNavigation.GoToVideoCapture(args);
                }
            }
        });
    }
}