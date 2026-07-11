using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Image.CaptureImage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Shared.Commands;
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
    private readonly ICaptureImageUseCase _captureImageCommand;
    private readonly IShutdownHandler _shutdownHandler;
    private readonly IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType> _captureTypeViewModelFactory;
    private readonly ITelemetryService? _telemetryService;

    private static readonly CaptureType[] _imageCaptureTypes = [
        CaptureType.Rectangle,
        CaptureType.Window,
        CaptureType.FullScreen,
        CaptureType.AllScreens,
    ];

    private static readonly CaptureType[] _videoCaptureTypes = [
        CaptureType.Rectangle,
        CaptureType.Window,
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

    public bool UsesCrosshairCursor
    {
        get;
        private set => Set(ref field, value);
    }

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

    private IList<WindowInfo> WindowInfos
    {
        get;
        set;
    } = [];

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
        ICaptureImageUseCase captureImageCommand,
        IThemeService themeService,
        IShutdownHandler shutdownHandler,
        IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode> captureModeViewModelFactory,
        IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType> captureTypeViewModelFactory,
        ITelemetryService? telemetryService = null)
    {
        _openImageEditCommand = openImageEditPageCommand;
        _openVideoCaptureOverlayCommand = openVideoCaptureOverlayCommand;
        _showMainWindowCommand = showMainWindowCommand;
        _captureImageCommand = captureImageCommand;
        _shutdownHandler = shutdownHandler;
        _captureTypeViewModelFactory = captureTypeViewModelFactory;
        _telemetryService = telemetryService;

        CaptureArea = Rectangle.Empty;
        MonitorWindows = [];

        DefaultAppTheme = themeService.DefaultTheme;
        CurrentAppTheme = themeService.CurrentTheme;

        RequestCaptureCommand = TelemetryCommandFactory.Async("selection_overlay.request_capture", RequestCaptureAsync, telemetryService, "selection_overlay");
        CloseOverlayCommand = TelemetryCommandFactory.Async("selection_overlay.close", CloseOverlayAsync, telemetryService, "selection_overlay");
        UpdateSelectedCaptureModeCommand = TelemetryCommandFactory.Relay<(int Index, SelectionUpdateSource Source)>("selection_overlay.update_capture_mode", UpdateSelectedCaptureMode, telemetryService, "selection_overlay");
        UpdateSelectedCaptureTypeCommand = TelemetryCommandFactory.Relay<(int Index, SelectionUpdateSource Source)>("selection_overlay.update_capture_type", UpdateSelectedCaptureType, telemetryService, "selection_overlay");
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
        WindowInfos = [.. options.MonitorWindows];
        MonitorWindows = [.. WindowInfos.Select(w => w.Position)];

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
            TrackException(exception, "selection_overlay.close");
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
        UpdateUsesCrosshairCursor();

        // Raise event with source information for propagation control
        CaptureTypeIndexChanged?.Invoke(this, args);
    }

    private void UpdateSupportedCaptureTypes()
    {
        _supportedCaptureTypes.Clear();
        if (SupportedCaptureModes.Count == 0)
        {
            SelectedCaptureTypeIndex = -1;
            UpdateUsesCrosshairCursor();
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

        CaptureType defaultCaptureType = GetDefaultCaptureType(GetSelectedCaptureMode());
        int defaultCaptureTypeIndex = SupportedCaptureTypes
            .Select((vm, index) => (vm.CaptureType, Index: index))
            .FirstOrDefault(item => item.CaptureType == defaultCaptureType).Index;

        SelectedCaptureTypeIndex = defaultCaptureTypeIndex;
        UpdateUsesCrosshairCursor();
        CaptureTypeIndexChanged?.Invoke(this, (defaultCaptureTypeIndex, SelectionUpdateSource.Programmatic));
    }

    private void UpdateUsesCrosshairCursor()
    {
        UsesCrosshairCursor = GetSelectedCaptureType() == CaptureType.Rectangle;
    }

    private static CaptureType GetDefaultCaptureType(CaptureMode? captureMode)
    {
        return captureMode switch
        {
            CaptureMode.Video => CaptureType.FullScreen,
            _ => CaptureType.Rectangle
        };
    }

    private async Task RequestCaptureAsync()
    {
        if (Monitor == null || CaptureArea == Rectangle.Empty)
        {
            return;
        }

        CaptureMode captureMode = SupportedCaptureModes[SelectedCaptureModeIndex].CaptureMode;
        CaptureType captureType = GetSelectedCaptureType() ?? GetDefaultCaptureType(captureMode);
        TrackCaptureEvent(TelemetryEvents.CaptureStarted, captureMode, captureType);

        if (captureMode == CaptureMode.Image)
        {
            NewCaptureArgs args = new(Monitor.Value, CaptureArea);
            ImageFile? image = (await _captureImageCommand.ExecuteAsync(new CaptureImageRequest(args), CancellationToken.None)).Value?.Image;
            if (image != null)
            {
                await _openImageEditCommand.ExecuteAsync(new OpenImageEditPageRequest(image), CancellationToken.None);
                TrackCaptureEvent(TelemetryEvents.CaptureCompleted, captureMode, captureType);
            }
            else
            {
                TrackCaptureEvent(TelemetryEvents.CaptureFailed, captureMode, captureType, "missing_image");
            }
        }
        else if (captureMode == CaptureMode.Video)
        {
            NewCaptureArgs args = new(Monitor.Value, CaptureArea, captureType, GetSelectedWindowHandle(captureType, CaptureArea));
            await _openVideoCaptureOverlayCommand.ExecuteAsync(new OpenCaptureOverlayRequest(args), CancellationToken.None);
        }
    }

    private nint GetSelectedWindowHandle(CaptureType captureType, Rectangle captureArea)
    {
        if (captureType != CaptureType.Window)
        {
            return 0;
        }

        return WindowInfos.FirstOrDefault(w => GetSelectableWindowRectangle(w.Position) == captureArea).Handle;
    }

    private static Rectangle GetSelectableWindowRectangle(Rectangle windowRect)
    {
        return new Rectangle(
            Math.Max(windowRect.X, 0),
            Math.Max(windowRect.Y, 0),
            windowRect.Width + Math.Min(windowRect.X, 0),
            windowRect.Height + Math.Min(windowRect.Y, 0));
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

    private void TrackCaptureEvent(
        string eventName,
        CaptureMode captureMode,
        CaptureType captureType,
        string? reasonCode = null)
    {
        Dictionary<string, object?> attributes = new()
        {
            [TelemetryAttributes.CaptureMode] = captureMode.ToString(),
            [TelemetryAttributes.CaptureType] = captureType.ToString(),
            [TelemetryAttributes.MediaType] = captureMode == CaptureMode.Video ? "video" : "image",
            [TelemetryAttributes.Surface] = "selection_overlay"
        };

        if (!string.IsNullOrWhiteSpace(reasonCode))
        {
            attributes[TelemetryAttributes.ReasonCode] = reasonCode;
        }

        _telemetryService?.TrackEvent(eventName, attributes);
    }

    private void TrackException(Exception exception, string commandId)
    {
        _telemetryService?.TrackException(
            exception,
            new TelemetryExceptionContext(
                Component: "SelectionOverlay",
                ActivityId: commandId,
                Attributes: new Dictionary<string, object?>
                {
                    [TelemetryAttributes.CommandId] = commandId,
                    [TelemetryAttributes.Surface] = "selection_overlay"
                }));
    }
}
