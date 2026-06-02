using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.Presentation.Features.SelectionOverlay;

public sealed partial class SelectionOverlayWindowViewModel : LoadableViewModelBase<SelectionOverlayWindowOptions>
{
    private readonly IOpenCaptureOverlayUseCase _openVideoCaptureOverlayCommand;
    private readonly IOpenImageEditPageUseCase _openImageEditCommand;
    private readonly IShowMainWindowUseCase _showMainWindowCommand;
    private readonly ITelemetryService _telemetryService;
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

    public IAsyncRelayCommand RequestCaptureCommand { get; }
    public IAsyncRelayCommand CloseOverlayCommand { get; }
    public IRelayCommand<(int Index, SelectionUpdateSource Source)> UpdateSelectedCaptureModeCommand { get; }
    public IRelayCommand<(int Index, SelectionUpdateSource Source)> UpdateSelectedCaptureTypeCommand { get; }
    public IRelayCommand<Rectangle> UpdateCaptureAreaCommand { get; }
    public IRelayCommand<CaptureOptions> UpdateCaptureOptionsCommand { get; }

    public event EventHandler<CaptureOptions>? CaptureOptionsUpdated;
    public event EventHandler<(int Index, SelectionUpdateSource Source)>? CaptureModeIndexChanged;
    public event EventHandler<(int Index, SelectionUpdateSource Source)>? CaptureTypeIndexChanged;

    public bool IsPrimary => Monitor?.IsPrimary ?? false;

    private ObservableCollection<CaptureTypeViewModel> _supportedCaptureTypes = [];

    public ObservableCollection<CaptureTypeViewModel> SupportedCaptureTypes
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

    private ObservableCollection<CaptureModeViewModel> _supportedCaptureModes = [];

    public ObservableCollection<CaptureModeViewModel> SupportedCaptureModes
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
        get;
        private set => Set(ref field, value);
    }

    public MonitorCaptureResult? Monitor
    {
        get;
        private set => Set(ref field, value);
    }

    public IList<Rectangle> MonitorWindows
    {
        get;
        private set => Set(ref field, value);
    }

    public AppTheme CurrentAppTheme
    {
        get;
        private set => Set(ref field, value);
    }

    public AppTheme DefaultAppTheme
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsDesktopAudioEnabled
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsCapturingVideo
    {
        get;
        private set => Set(ref field, value);
    }

    public SelectionOverlayWindowViewModel(
        IOpenImageEditPageUseCase openImageEditPageCommand,
        IOpenCaptureOverlayUseCase openVideoCaptureOverlayCommand,
        IShowMainWindowUseCase showMainWindowCommand,
        ITelemetryService telemetryService,
        IThemeService themeService,
        IShutdownHandler shutdownHandler,
        IImageCaptureHandler imageCaptureHandler,
        IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode> captureModeViewModelFactory,
        IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType> captureTypeViewModelFactory)
    {
        _openImageEditCommand = openImageEditPageCommand;
        _openVideoCaptureOverlayCommand = openVideoCaptureOverlayCommand;
        _showMainWindowCommand = showMainWindowCommand;
        _telemetryService = telemetryService;
        _shutdownHandler = shutdownHandler;
        _imageCaptureHandler = imageCaptureHandler;
        _captureTypeViewModelFactory = captureTypeViewModelFactory;

        CaptureArea = Rectangle.Empty;
        MonitorWindows = [];

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;

        RequestCaptureCommand = new AsyncRelayCommand(RequestCaptureAsync);
        CloseOverlayCommand = new AsyncRelayCommand(CloseOverlayAsync);
        UpdateSelectedCaptureModeCommand = new RelayCommand<(int Index, SelectionUpdateSource Source)>(UpdateSelectedCaptureMode);
        UpdateSelectedCaptureTypeCommand = new RelayCommand<(int Index, SelectionUpdateSource Source)>(UpdateSelectedCaptureType);
        UpdateCaptureAreaCommand = new RelayCommand<Rectangle>(UpdateCaptureArea);
        UpdateCaptureOptionsCommand = new RelayCommand<CaptureOptions>(UpdateCaptureOptions);

        CaptureModeViewModel imageModeVM = captureModeViewModelFactory.Create(CaptureMode.Image);
        _supportedCaptureModes.Add(imageModeVM);

        CaptureModeViewModel videoModeVM = captureModeViewModelFactory.Create(CaptureMode.Video);
        _supportedCaptureModes.Add(videoModeVM);

        IsDesktopAudioEnabled = true;
    }

    public override void Load(SelectionOverlayWindowOptions options)
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
    }

    private async Task CloseOverlayAsync()
    {
        try
        {
            await _showMainWindowCommand.ExecuteAsync(new ShowMainWindowRequest(), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(CloseOverlayAsync), exception);
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
            SelectedCaptureTypeIndex = -1;
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

        SelectedCaptureTypeIndex = 0;
        CaptureTypeIndexChanged?.Invoke(this, (0, SelectionUpdateSource.Programmatic));
    }

    private async Task RequestCaptureAsync()
    {
        try
        {
            if (Monitor != null && CaptureArea != Rectangle.Empty)
            {
                if (SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode == CaptureMode.Image)
                {
                    NewCaptureArgs args = new(Monitor.Value, CaptureArea);
                    ImageFile image = _imageCaptureHandler.PerformImageCapture(args);
                    await _openImageEditCommand.ExecuteAsync(new OpenImageEditPageRequest(image), CancellationToken.None);

                }
                else if (SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode == CaptureMode.Video)
                {
                    NewCaptureArgs args = new(Monitor.Value, CaptureArea);
                    await _openVideoCaptureOverlayCommand.ExecuteAsync(new OpenCaptureOverlayRequest(args), CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(RequestCaptureAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(RequestCaptureAsync), exception);
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
