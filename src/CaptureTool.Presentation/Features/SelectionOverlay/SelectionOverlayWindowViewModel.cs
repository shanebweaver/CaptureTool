using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Domain.Capture;
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

    public ObservableCollection<CaptureTypeViewModel> SupportedCaptureTypes
    {
        get;
        private set => Set(ref field, value);
    } = [];

    public int SelectedCaptureTypeIndex
    {
        get;
        private set => Set(ref field, value);
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

    public ObservableCollection<CaptureModeViewModel> SupportedCaptureModes
    {
        get;
        private set => Set(ref field, value);
    } = [];

    public int SelectedCaptureModeIndex
    {
        get;
        private set => Set(ref field, value);
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
        SupportedCaptureModes.Add(imageModeVM);

        CaptureModeViewModel videoModeVM = captureModeViewModelFactory.Create(CaptureMode.Video);
        SupportedCaptureModes.Add(videoModeVM);

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
        UpdateSelectedCaptureMode((SupportedCaptureModes.IndexOf(targetMode), SelectionUpdateSource.Programmatic));

        var targetType = SupportedCaptureTypes.First(vm => vm.CaptureType == options.CaptureOptions.CaptureType);
        UpdateSelectedCaptureType((SupportedCaptureTypes.IndexOf(targetType), SelectionUpdateSource.Programmatic));

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
        UpdateSelectedCaptureMode((SupportedCaptureModes.IndexOf(targetMode), SelectionUpdateSource.Programmatic));

        var targetType = SupportedCaptureTypes.First(vm => vm.CaptureType == options.CaptureType);
        UpdateSelectedCaptureType((SupportedCaptureTypes.IndexOf(targetType), SelectionUpdateSource.Programmatic));

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
        SupportedCaptureTypes.Clear();
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
            SupportedCaptureTypes.Add(_captureTypeViewModelFactory.Create(supportedCaptureType));
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
                    CaptureType captureType = GetSelectedCaptureType() ?? CaptureType.FullScreen;
                    NewCaptureArgs args = new(Monitor.Value, CaptureArea, captureType, GetSelectedWindowHandle(captureType, CaptureArea));
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
        SupportedCaptureTypes.Clear();
        SupportedCaptureModes.Clear();

        base.Dispose();
    }
}
