using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.FeatureManagement;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Infrastructure.Interfaces.Themes;
using CaptureTool.ViewModels.Helpers;
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

    private const string TelemetryContext = "SelectionOverlayWindow";

    private readonly ITelemetryService _telemetryService;
    private readonly IAppNavigation _appNavigation;
    private readonly IShutdownHandler _shutdownHandler;
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
    public RelayCommand<CaptureOptions> UpdateCaptureOptionsCommand { get; }

    public event EventHandler<CaptureOptions>? CaptureOptionsUpdated;

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
        => SelectedCaptureTypeIndex != -1 && SelectedCaptureTypeIndex < SupportedCaptureTypes.Count 
            ? SupportedCaptureTypes[SelectedCaptureTypeIndex].CaptureType 
            : null;

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

    public CaptureMode? GetSelectedCaptureMode()
        => SelectedCaptureModeIndex != -1 && SelectedCaptureModeIndex < SupportedCaptureModes.Count
            ? SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode 
            : null;

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
        IShutdownHandler shutdownHandler,
        IImageCaptureHandler imageCaptureHandler,
        IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode> captureModeViewModelFactory,
        IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType> captureTypeViewModelFactory)
    {
        _telemetryService = telemetryService;
        _appNavigation = appNavigation;
        _shutdownHandler = shutdownHandler;
        _imageCaptureHandler = imageCaptureHandler;
        _captureTypeViewModelFactory = captureTypeViewModelFactory;
        
        CaptureArea = Rectangle.Empty;
        MonitorWindows = [];

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;

        TelemetryCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        RequestCaptureCommand = commandFactory.Create(ActivityIds.RequestCapture, RequestCapture);
        CloseOverlayCommand = commandFactory.Create(ActivityIds.CloseOverlay, CloseOverlay);
        UpdateSelectedCaptureModeCommand = new(UpdateSelectedCaptureMode);
        UpdateSelectedCaptureTypeCommand = new(UpdateSelectedCaptureType);
        UpdateCaptureAreaCommand = new(UpdateCaptureArea);
        UpdateCaptureOptionsCommand = new(UpdateCaptureOptions);

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
        TelemetryHelper.ExecuteActivity(_telemetryService, TelemetryContext, ActivityIds.Load, () =>
        {
            ThrowIfNotReadyToLoad();
            StartLoading();

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
        if (_appNavigation.CanGoBack)
        {
            _appNavigation.GoBackToMainWindow();
        }
        else
        {
            _shutdownHandler.Shutdown();
        }
    }

    private void UpdateCaptureArea(Rectangle area)
    {
        CaptureArea = area;
    }

    private void UpdateCaptureOptions(CaptureOptions options)
    {
        var targetMode = SupportedCaptureModes.First(vm => vm.CaptureMode == options.CaptureMode);
        UpdateSelectedCaptureMode(SupportedCaptureModes.IndexOf(targetMode));

        UpdateSupportedCaptureTypes();

        var targetType = SupportedCaptureTypes.First(vm => vm.CaptureType == options.CaptureType);
        UpdateSelectedCaptureType(SupportedCaptureTypes.IndexOf(targetType));

        UpdateCaptureArea(Rectangle.Empty);

        CaptureOptionsUpdated?.Invoke(this, options);
    }

    private void UpdateSelectedCaptureMode(int index)
    {
        SelectedCaptureModeIndex = index;
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

        var supportedCaptureTypes = GetSelectedCaptureMode() switch
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
    }

    public override void Dispose()
    {
        // Explicitly null Monitor to release the ~100MB PixelBuffer reference
        Monitor = null;
        MonitorWindows = [];
        
        // Clear collections to release any remaining references
        SupportedCaptureTypes.Clear();
        SupportedCaptureModes.Clear();
        
        base.Dispose();
    }
}