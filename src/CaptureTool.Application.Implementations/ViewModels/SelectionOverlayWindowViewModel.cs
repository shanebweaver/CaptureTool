using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Factories;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Infrastructure.Interfaces.Themes;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class SelectionOverlayWindowViewModel : LoadableViewModelBase<SelectionOverlayWindowOptions>, ISelectionOverlayWindowViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadSelectionOverlayWindow";
        public static readonly string RequestCapture = "RequestCapture";
        public static readonly string CloseOverlay = "CloseOverlay";
        public static readonly string UpdateSelectedCaptureMode = "UpdateSelectedCaptureMode";
        public static readonly string UpdateSelectedCaptureType = "UpdateSelectedCaptureType";
        public static readonly string UpdateCaptureArea = "UpdateCaptureArea";
        public static readonly string UpdateCaptureOptions = "UpdateCaptureOptions";
    }

    private const string TelemetryContext = "SelectionOverlayWindow";

    private readonly ITelemetryService _telemetryService;
    private readonly IAppNavigation _appNavigation;
    private readonly IShutdownHandler _shutdownHandler;
    private readonly IImageCaptureHandler _imageCaptureHandler;
    private readonly IFactoryServiceWithArgs<ICaptureTypeViewModel, CaptureType> _captureTypeViewModelFactory;

    private static readonly CaptureType[] _imageCaptureTypes = [
        CaptureType.Rectangle,
        CaptureType.Window,
        CaptureType.FullScreen,
        CaptureType.AllScreens,
    ];

    private static readonly CaptureType[] _videoCaptureTypes = [
        CaptureType.FullScreen,
    ];

    public IAppCommand RequestCaptureCommand { get; }
    public IAppCommand CloseOverlayCommand { get; }
    public IAppCommand<(int Index, SelectionUpdateSource Source)> UpdateSelectedCaptureModeCommand { get; }
    public IAppCommand<(int Index, SelectionUpdateSource Source)> UpdateSelectedCaptureTypeCommand { get; }
    public IAppCommand<Rectangle> UpdateCaptureAreaCommand { get; }
    public IAppCommand<CaptureOptions> UpdateCaptureOptionsCommand { get; }

    public event EventHandler<CaptureOptions>? CaptureOptionsUpdated;
    public event EventHandler<(int Index, SelectionUpdateSource Source)>? CaptureModeIndexChanged;
    public event EventHandler<(int Index, SelectionUpdateSource Source)>? CaptureTypeIndexChanged;

    public bool IsPrimary => Monitor?.IsPrimary ?? false;

    private ObservableCollection<ICaptureTypeViewModel> _supportedCaptureTypes = [];

    public ObservableCollection<ICaptureTypeViewModel> SupportedCaptureTypes
    {
        get => _supportedCaptureTypes;
        private set
        {
            _supportedCaptureTypes = value;
            RaisePropertyChanged(nameof(SupportedCaptureTypes));
        }
    }

    private int _selectedCaptureTypeIndex;

    public int SelectedCaptureTypeIndex
    {
        get => _selectedCaptureTypeIndex;
        private set => Set(ref _selectedCaptureTypeIndex, value);
    }

    public CaptureType? GetSelectedCaptureType()
        => SelectedCaptureTypeIndex != -1 && SelectedCaptureTypeIndex < SupportedCaptureTypes.Count
            ? SupportedCaptureTypes[SelectedCaptureTypeIndex].CaptureType
            : null;

    private ObservableCollection<ICaptureModeViewModel> _supportedCaptureModes = [];

    public ObservableCollection<ICaptureModeViewModel> SupportedCaptureModes
    {
        get => _supportedCaptureModes;
        private set
        {
            _supportedCaptureModes = value;
            RaisePropertyChanged(nameof(SupportedCaptureModes));
        }
    }

    private int _selectedCaptureModeIndex;

    public int SelectedCaptureModeIndex
    {
        get => _selectedCaptureModeIndex;
        private set => Set(ref _selectedCaptureModeIndex, value);
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
        IFactoryServiceWithArgs<ICaptureModeViewModel, CaptureMode> captureModeViewModelFactory,
        IFactoryServiceWithArgs<ICaptureTypeViewModel, CaptureType> captureTypeViewModelFactory)
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

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        RequestCaptureCommand = commandFactory.Create(ActivityIds.RequestCapture, RequestCapture);
        CloseOverlayCommand = commandFactory.Create(ActivityIds.CloseOverlay, CloseOverlay);
        UpdateSelectedCaptureModeCommand = commandFactory.Create<(int Index, SelectionUpdateSource Source)>(ActivityIds.UpdateSelectedCaptureMode, UpdateSelectedCaptureMode);
        UpdateSelectedCaptureTypeCommand = commandFactory.Create<(int Index, SelectionUpdateSource Source)>(ActivityIds.UpdateSelectedCaptureType, UpdateSelectedCaptureType);
        UpdateCaptureAreaCommand = commandFactory.Create<Rectangle>(ActivityIds.UpdateCaptureArea, UpdateCaptureArea);
        UpdateCaptureOptionsCommand = commandFactory.Create<CaptureOptions>(ActivityIds.UpdateCaptureOptions, UpdateCaptureOptions);

        IsVideoCaptureFeatureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);

        ICaptureModeViewModel imageModeVM = captureModeViewModelFactory.Create(CaptureMode.Image);
        _supportedCaptureModes.Add(imageModeVM);
        if (IsVideoCaptureFeatureEnabled)
        {
            ICaptureModeViewModel videoModeVM = captureModeViewModelFactory.Create(CaptureMode.Video);
            _supportedCaptureModes.Add(videoModeVM);
        }

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
            UpdateSelectedCaptureMode((_supportedCaptureModes.IndexOf(targetMode), SelectionUpdateSource.Programmatic));

            var targetType = SupportedCaptureTypes.First(vm => vm.CaptureType == options.CaptureOptions.CaptureType);
            UpdateSelectedCaptureType((_supportedCaptureTypes.IndexOf(targetType), SelectionUpdateSource.Programmatic));

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
        UpdateSelectedCaptureMode((_supportedCaptureModes.IndexOf(targetMode), SelectionUpdateSource.Programmatic));

        var targetType = SupportedCaptureTypes.First(vm => vm.CaptureType == options.CaptureType);
        UpdateSelectedCaptureType((_supportedCaptureTypes.IndexOf(targetType), SelectionUpdateSource.Programmatic));

        UpdateCaptureArea(Rectangle.Empty);

        CaptureOptionsUpdated?.Invoke(this, options);
    }

    private void UpdateSelectedCaptureMode((int Index, SelectionUpdateSource Source) args)
    {
        SelectedCaptureModeIndex = args.Index;
        UpdateSupportedCaptureTypes();

        // Raise event with source information for propagation control
        CaptureModeIndexChanged?.Invoke(this, args);
    }

    private void UpdateSelectedCaptureType((int Index, SelectionUpdateSource Source) args)
    {
        SelectedCaptureTypeIndex = args.Index;

        // Raise event with source information for propagation control
        CaptureTypeIndexChanged?.Invoke(this, args);
    }

    private void UpdateSupportedCaptureTypes()
    {
        _supportedCaptureTypes.Clear();
        if (SupportedCaptureModes.Count == 0)
        {
            _selectedCaptureTypeIndex = -1;
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
            _supportedCaptureTypes.Add(_captureTypeViewModelFactory.Create(supportedCaptureType));
        }

        _selectedCaptureTypeIndex = 0;
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
        _supportedCaptureTypes.Clear();
        _supportedCaptureModes.Clear();

        base.Dispose();
    }
}