using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Ai;
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed partial class ImageEditPageViewModel : AsyncLoadableViewModelBase<ImageFile>, IEditableSession
{
    private enum CanvasUpdateMode
    {
        InvalidateLayout,
        Redraw,
        ReloadResources,
    }

    private const string ImageDescriptionCopiedMessageResourceKey = "ImageDescriptionCopiedNotification";
    private const string ImageDescriptionCopyFailedMessageResourceKey = "ImageDescriptionCopyFailedNotification";

    private readonly ILocalizationService _localizationService;
    private readonly ICancellationService _cancellationService;
    private readonly IImageCanvasPrinter _imageCanvasPrinter;
    private readonly IImageCanvasExporter _imageCanvasExporter;
    private readonly IFilePickerService _filePickerService;
    private readonly IImageMetadataService _imageMetadataService;
    private readonly IImageSuperResolutionService _imageSuperResolutionService;
    private readonly IImageSuperResolutionFeatureAvailability _imageSuperResolutionFeatureAvailability;
    private readonly IImageSuperResolutionPreparationConsentService _imageSuperResolutionPreparationConsentService;
    private readonly ITextExtractionService _textExtractionService;
    private readonly ITextExtractionFeatureAvailability _textExtractionFeatureAvailability;
    private readonly IImageDescriptionService _imageDescriptionService;
    private readonly IImageDescriptionFeatureAvailability _imageDescriptionFeatureAvailability;
    private readonly IImageForegroundExtractionService _imageForegroundExtractionService;
    private readonly IImageForegroundExtractionFeatureAvailability _imageForegroundExtractionFeatureAvailability;
    private readonly IImageObjectEraseService _imageObjectEraseService;
    private readonly IImageObjectEraseFeatureAvailability _imageObjectEraseFeatureAvailability;
    private readonly IImageObjectExtractionFeatureAvailability _imageObjectExtractionFeatureAvailability;
    private readonly IAiFeatureConsentService _aiFeatureConsentService;
    private readonly IAiFeatureConsentDialogService _aiFeatureConsentDialogService;
    private readonly IShareService _shareService;
    private readonly IOpenExternalEditorUseCase _openExternalEditorAction;
    private readonly IStorageService _storageService;
    private readonly ISettingsService _settingsService;
    private readonly IOpenScreenshotsFolderUseCase _openScreenshotsFolderAction;
    private readonly ILogService _logService;
    private readonly IAppNotificationService _notificationService;
    private readonly IClipboardService _clipboardService;
    private readonly ITelemetryService? _telemetryService;

    private readonly ImageEditHistory _editHistory;
    private readonly ImageEditModeStateMachine _modeStateMachine;
    private ImageDrawable? _imageDrawable;
    private ImageEditSession _editSession;
    private ImageFile? _originalImageFile;
    private Size _originalImageSize;
    private ImageFile? _superResolutionImageFile;
    private Size _superResolutionImageSize;
    private CancellationTokenSource? _superResolutionCancellationTokenSource;
    private CancellationTokenSource? _textExtractionCancellationTokenSource;
    private CancellationTokenSource? _imageDescriptionCancellationTokenSource;
    private CancellationTokenSource? _foregroundExtractionCancellationTokenSource;
    private CancellationTokenSource? _objectEraseCancellationTokenSource;
    private CancellationTokenSource? _objectExtractionCancellationTokenSource;
    private ImageDescriptionMode? _runningImageDescriptionMode;
    private readonly Dictionary<ImageDescriptionMode, string> _imageDescriptionResults = [];
    private bool _hasUnsavedChangesBeforeSuperResolution;
    private bool _hasUserEditsSinceSuperResolutionActivated;
    private int _editRevision;
    private int? _textExtractionProcessedRevision;

    public event EventHandler? InvalidateCanvasRequested;
    public event EventHandler? RedrawCanvasRequested;
    public event EventHandler? ReloadCanvasResourcesRequested;
    public event EventHandler? ForceZoomAndCenterRequested;

    public IAsyncRelayCommand CopyCommand { get; }
    public IRelayCommand ToggleCropModeCommand { get; }
    public IRelayCommand ToggleShapesModeCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand OpenScreenshotsFolderCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand RotateCommand { get; }
    public IRelayCommand FlipHorizontalCommand { get; }
    public IRelayCommand FlipVerticalCommand { get; }
    public IAsyncRelayCommand PrintCommand { get; }
    public IAsyncRelayCommand ShareCommand { get; }
    public IAsyncRelayCommand EditInPaintCommand { get; }
    public IAsyncRelayCommand ToggleSuperResolutionCommand { get; }
    public IAsyncRelayCommand ToggleTextExtractionModeCommand { get; }
    public IAsyncRelayCommand ToggleImageDescriptionModeCommand { get; }
    public IAsyncRelayCommand ToggleForegroundExtractionModeCommand { get; }
    public IAsyncRelayCommand ToggleObjectEraseModeCommand { get; }
    public IAsyncRelayCommand ToggleObjectExtractionModeCommand { get; }
    public IAsyncRelayCommand GenerateBriefImageDescriptionCommand { get; }
    public IAsyncRelayCommand GenerateDetailedImageDescriptionCommand { get; }
    public IAsyncRelayCommand GenerateDiagramImageDescriptionCommand { get; }
    public IAsyncRelayCommand GenerateAccessibleImageDescriptionCommand { get; }
    public IAsyncRelayCommand CopyImageDescriptionCommand { get; }
    public IRelayCommand<ImageOrientation> UpdateOrientationCommand { get; }
    public IRelayCommand<Rectangle> UpdateCropRectCommand { get; }
    public IRelayCommand<bool> SetChromaKeyModeActiveCommand { get; }
    public IRelayCommand ToggleTextModeCommand { get; }
    public IRelayCommand ToggleColorPickerModeCommand { get; }
    public IRelayCommand<int> UpdateZoomPercentageCommand { get; }
    public IRelayCommand<bool> UpdateAutoZoomLockCommand { get; }
    public IRelayCommand ZoomAndCenterCommand { get; }

    public ChromaKeyToolViewModel ChromaKeyTool { get; }

    public ColorPickerToolViewModel ColorPickerTool { get; }

    public ShapeToolViewModel ShapeTool { get; }

    public TextToolViewModel TextTool { get; }

    public TextExtractionToolViewModel TextExtractionTool { get; }

    public bool HasUndoStack
    {
        get;
        private set => Set(ref field, value);
    }

    public bool HasRedoStack
    {
        get;
        private set => Set(ref field, value);
    }

    public IReadOnlyList<IDrawable> Drawables
    {
        get;
        private set => Set(ref field, value);
    }

    public ImageFile? ImageFile
    {
        get;
        private set => Set(ref field, value);
    }

    public Size ImageSize
    {
        get;
        private set => Set(ref field, value);
    }

    public ImageOrientation Orientation
    {
        get;
        private set => Set(ref field, value);
    }

    public string MirroredDisplayName
    {
        get;
        private set => Set(ref field, value);
    }

    public string RotationDisplayName
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsCropModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsShapesModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsTextModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public Rectangle CropRect
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsChromaKeyModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsColorPickerModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsTextExtractionModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsImageDescriptionModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsForegroundExtractionModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsForegroundExtractionFeatureEnabled
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleForegroundExtraction();
            }
        }
    }

    public bool IsForegroundExtractionAvailable
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleForegroundExtraction();
            }
        }
    }

    public bool IsForegroundExtractionRunning
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleForegroundExtraction();
            }
        }
    }

    public bool CanToggleForegroundExtraction
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                ToggleForegroundExtractionModeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ForegroundExtractionStatusMessage
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public bool IsObjectEraseModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsObjectEraseFeatureEnabled
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleObjectErase();
            }
        }
    }

    public bool IsObjectEraseAvailable
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleObjectErase();
            }
        }
    }

    public bool IsObjectEraseRunning
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleObjectErase();
            }
        }
    }

    public bool CanToggleObjectErase
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                ToggleObjectEraseModeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ObjectEraseStatusMessage
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public bool IsObjectExtractionModeActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsObjectExtractionFeatureEnabled
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleObjectExtraction();
            }
        }
    }

    public bool IsObjectExtractionAvailable
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleObjectExtraction();
            }
        }
    }

    public bool IsObjectExtractionRunning
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleObjectExtraction();
            }
        }
    }

    public bool CanToggleObjectExtraction
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                ToggleObjectExtractionModeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ObjectExtractionStatusMessage
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public bool IsSuperResolutionAvailable
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleSuperResolution();
            }
        }
    }

    public bool IsSuperResolutionFeatureEnabled
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleSuperResolution();
            }
        }
    }

    public bool IsSuperResolutionActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsSuperResolutionGenerating
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleSuperResolution();
            }
        }
    }

    public bool CanToggleSuperResolution
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                ToggleSuperResolutionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SuperResolutionStatusMessage
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public bool IsTextExtractionFeatureEnabled
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleTextExtraction();
            }
        }
    }

    public bool IsTextExtractionAvailable
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleTextExtraction();
            }
        }
    }

    public bool IsTextExtractionRunning
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleTextExtraction();
            }
        }
    }

    public bool CanToggleTextExtraction
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                ToggleTextExtractionModeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string TextExtractionStatusMessage
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public IReadOnlyList<RecognizedTextRegion> TextExtractionRegions
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                HasTextExtractionRegions = value.Count > 0;
            }
        }
    } = [];

    public bool HasTextExtractionRegions
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsImageDescriptionFeatureEnabled
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleImageDescription();
            }
        }
    }

    public bool IsImageDescriptionAvailable
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleImageDescription();
            }
        }
    }

    public bool IsImageDescriptionRunning
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                UpdateCanToggleImageDescription();
                UpdateCanGenerateImageDescription();
            }
        }
    }

    public bool CanToggleImageDescription
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                ToggleImageDescriptionModeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanGenerateImageDescription
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                GenerateBriefImageDescriptionCommand.NotifyCanExecuteChanged();
                GenerateDetailedImageDescriptionCommand.NotifyCanExecuteChanged();
                GenerateDiagramImageDescriptionCommand.NotifyCanExecuteChanged();
                GenerateAccessibleImageDescriptionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ImageDescription
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                HasImageDescription = !string.IsNullOrWhiteSpace(value);
            }
        }
    } = string.Empty;

    public bool HasImageDescription
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                CopyImageDescriptionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ImageDescriptionMode? SelectedImageDescriptionMode
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaiseImageDescriptionSelectionProperties();
            }
        }
    }

    public bool IsBriefImageDescriptionSelected =>
        SelectedImageDescriptionMode == ImageDescriptionMode.Brief;

    public bool IsDetailedImageDescriptionSelected =>
        SelectedImageDescriptionMode == ImageDescriptionMode.Detailed;

    public bool IsDiagramImageDescriptionSelected =>
        SelectedImageDescriptionMode == ImageDescriptionMode.Diagram;

    public bool IsAccessibleImageDescriptionSelected =>
        SelectedImageDescriptionMode == ImageDescriptionMode.Accessible;

    public string ImageDescriptionStatusMessage
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public int ZoomPercentage
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsAutoZoomLocked
    {
        get;
        private set => Set(ref field, value);
    }

    public string EditSessionName => "image edit session";

    public bool HasUnsavedChanges
    {
        get;
        private set => Set(ref field, value);
    }

    public ImageEditPageViewModel(
        ILocalizationService localizationService,
        ICancellationService cancellationService,
        IImageCanvasPrinter imageCanvasPrinter,
        IImageCanvasExporter imageCanvasExporter,
        IFilePickerService filePickerService,
        IImageMetadataService imageMetadataService,
        IImageSuperResolutionService imageSuperResolutionService,
        IImageSuperResolutionFeatureAvailability imageSuperResolutionFeatureAvailability,
        IImageSuperResolutionPreparationConsentService imageSuperResolutionPreparationConsentService,
        IShareService shareService,
        IOpenExternalEditorUseCase openExternalEditorAction,
        IStorageService storageService,
        ISettingsService settingsService,
        IOpenScreenshotsFolderUseCase openScreenshotsFolderAction,
        ILogService logService,
        IAppNotificationService notificationService,
        IClipboardService clipboardService,
        ColorPickerToolViewModel colorPickerTool,
        ChromaKeyToolViewModel chromaKeyTool,
        ShapeToolViewModel shapeTool,
        TextToolViewModel textTool,
        TextExtractionToolViewModel textExtractionTool,
        IAiFeatureConsentService? aiFeatureConsentService = null,
        IAiFeatureConsentDialogService? aiFeatureConsentDialogService = null,
        ITextExtractionService? textExtractionService = null,
        ITextExtractionFeatureAvailability? textExtractionFeatureAvailability = null,
        IImageDescriptionService? imageDescriptionService = null,
        IImageDescriptionFeatureAvailability? imageDescriptionFeatureAvailability = null,
        IImageForegroundExtractionService? imageForegroundExtractionService = null,
        IImageForegroundExtractionFeatureAvailability? imageForegroundExtractionFeatureAvailability = null,
        IImageObjectEraseService? imageObjectEraseService = null,
        IImageObjectEraseFeatureAvailability? imageObjectEraseFeatureAvailability = null,
        IImageObjectExtractionFeatureAvailability? imageObjectExtractionFeatureAvailability = null,
        ITelemetryService? telemetryService = null)
    {
        _localizationService = localizationService;
        _cancellationService = cancellationService;
        _imageCanvasPrinter = imageCanvasPrinter;
        _filePickerService = filePickerService;
        _imageMetadataService = imageMetadataService;
        _imageSuperResolutionService = imageSuperResolutionService;
        _imageSuperResolutionFeatureAvailability = imageSuperResolutionFeatureAvailability;
        _imageSuperResolutionPreparationConsentService = imageSuperResolutionPreparationConsentService;
        _textExtractionService = textExtractionService ?? new NullTextExtractionService();
        _textExtractionFeatureAvailability = textExtractionFeatureAvailability ?? new DisabledTextExtractionFeatureAvailability();
        _imageDescriptionService = imageDescriptionService ?? new NullImageDescriptionService();
        _imageDescriptionFeatureAvailability = imageDescriptionFeatureAvailability ?? new DisabledImageDescriptionFeatureAvailability();
        _imageForegroundExtractionService = imageForegroundExtractionService ?? new NullImageForegroundExtractionService();
        _imageForegroundExtractionFeatureAvailability = imageForegroundExtractionFeatureAvailability ?? new DisabledImageForegroundExtractionFeatureAvailability();
        _imageObjectEraseService = imageObjectEraseService ?? new NullImageObjectEraseService();
        _imageObjectEraseFeatureAvailability = imageObjectEraseFeatureAvailability ?? new DisabledImageObjectEraseFeatureAvailability();
        _imageObjectExtractionFeatureAvailability = imageObjectExtractionFeatureAvailability ?? new DisabledImageObjectExtractionFeatureAvailability();
        _aiFeatureConsentService = aiFeatureConsentService ?? new PermissiveAiFeatureConsentService();
        _aiFeatureConsentDialogService = aiFeatureConsentDialogService ?? new PermissiveAiFeatureConsentDialogService();
        _shareService = shareService;
        _openExternalEditorAction = openExternalEditorAction;
        _storageService = storageService;
        _imageCanvasExporter = imageCanvasExporter;
        _settingsService = settingsService;
        _openScreenshotsFolderAction = openScreenshotsFolderAction;
        _logService = logService;
        _notificationService = notificationService;
        _clipboardService = clipboardService;
        _telemetryService = telemetryService;

        ChromaKeyTool = chromaKeyTool;
        ColorPickerTool = colorPickerTool;
        ShapeTool = shapeTool;
        TextTool = textTool;
        TextExtractionTool = textExtractionTool;
        ChromaKeyTool.SettingsChanged += ChromaKeyTool_SettingsChanged;
        ChromaKeyTool.InteractionCommitted += ChromaKeyTool_InteractionCommitted;

        _editHistory = new ImageEditHistory();
        _modeStateMachine = new ImageEditModeStateMachine();
        _editSession = new ImageEditSession(Size.Empty, ImageOrientation.RotateNoneFlipNone, Rectangle.Empty);
        Drawables = _editSession.Drawables;
        ImageSize = _editSession.ImageSize;
        CropRect = _editSession.CropRect;
        Orientation = _editSession.Orientation;
        MirroredDisplayName = string.Empty;
        RotationDisplayName = string.Empty;
        ZoomPercentage = 100;
        IsSuperResolutionFeatureEnabled = _imageSuperResolutionFeatureAvailability.IsImageSuperResolutionEnabled;
        IsTextExtractionFeatureEnabled = _textExtractionFeatureAvailability.IsTextExtractionEnabled;
        IsImageDescriptionFeatureEnabled = _imageDescriptionFeatureAvailability.IsImageDescriptionEnabled;
        IsForegroundExtractionFeatureEnabled = _imageForegroundExtractionFeatureAvailability.IsImageForegroundExtractionEnabled;
        IsObjectEraseFeatureEnabled = _imageObjectEraseFeatureAvailability.IsImageObjectEraseEnabled;
        IsObjectExtractionFeatureEnabled = _imageObjectExtractionFeatureAvailability.IsImageObjectExtractionEnabled;
        _settingsService.SettingsChanged += SettingsService_SettingsChanged;

        CopyCommand = new AsyncRelayCommand(CopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleCropModeCommand = new RelayCommand(ToggleCropMode);
        ToggleShapesModeCommand = new RelayCommand(ToggleShapesMode);
        SaveCommand = new AsyncRelayCommand(SaveCommandAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenScreenshotsFolderCommand = new AsyncRelayCommand(OpenScreenshotsFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UndoCommand = new RelayCommand(Undo);
        RedoCommand = new RelayCommand(Redo);
        RotateCommand = new RelayCommand(Rotate);
        FlipHorizontalCommand = new RelayCommand(() => Flip(FlipDirection.Horizontal));
        FlipVerticalCommand = new RelayCommand(() => Flip(FlipDirection.Vertical));
        PrintCommand = new AsyncRelayCommand(PrintAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ShareCommand = new AsyncRelayCommand(ShareAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        EditInPaintCommand = new AsyncRelayCommand(EditInPaintAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleSuperResolutionCommand = new AsyncRelayCommand(
            ToggleSuperResolutionAsync,
            () => CanToggleSuperResolution,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleTextExtractionModeCommand = new AsyncRelayCommand(
            ToggleTextExtractionModeAsync,
            () => CanToggleTextExtraction,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleImageDescriptionModeCommand = new AsyncRelayCommand(
            ToggleImageDescriptionModeAsync,
            () => CanToggleImageDescription,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleForegroundExtractionModeCommand = new AsyncRelayCommand(
            ToggleForegroundExtractionModeAsync,
            () => CanToggleForegroundExtraction,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleObjectEraseModeCommand = new AsyncRelayCommand(
            ToggleObjectEraseModeAsync,
            () => CanToggleObjectErase,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleObjectExtractionModeCommand = new AsyncRelayCommand(
            ToggleObjectExtractionModeAsync,
            () => CanToggleObjectExtraction,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        GenerateBriefImageDescriptionCommand = CreateImageDescriptionCommand(ImageDescriptionMode.Brief);
        GenerateDetailedImageDescriptionCommand = CreateImageDescriptionCommand(ImageDescriptionMode.Detailed);
        GenerateDiagramImageDescriptionCommand = CreateImageDescriptionCommand(ImageDescriptionMode.Diagram);
        GenerateAccessibleImageDescriptionCommand = CreateImageDescriptionCommand(ImageDescriptionMode.Accessible);
        CopyImageDescriptionCommand = new AsyncRelayCommand(
            CopyImageDescriptionAsync,
            () => HasImageDescription,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateOrientationCommand = new RelayCommand<ImageOrientation>(UpdateOrientation);
        UpdateCropRectCommand = new RelayCommand<Rectangle>(UpdateCropRect);
        SetChromaKeyModeActiveCommand = new RelayCommand<bool>(SetChromaKeyModeActive);
        ToggleTextModeCommand = new RelayCommand(ToggleTextMode);
        ToggleColorPickerModeCommand = new RelayCommand(ToggleColorPickerMode);
        UpdateZoomPercentageCommand = new RelayCommand<int>(UpdateZoomPercentage);
        UpdateAutoZoomLockCommand = new RelayCommand<bool>(UpdateAutoZoomLock);
        ZoomAndCenterCommand = new RelayCommand(RequestZoomAndCenter);
    }

    private void ChromaKeyTool_SettingsChanged(object? sender, EventArgs e)
    {
        UpdateChromaKeyEffectValues();
    }

    private void ChromaKeyTool_InteractionCommitted(
        object? sender,
        (ChromaKeySettings OldSettings, ChromaKeySettings NewSettings) settings)
    {
        ExecuteEditCommand(
            new SetChromaKeyCommand(settings.OldSettings, settings.NewSettings),
            CanvasUpdateMode.Redraw);
    }

    private void SettingsService_SettingsChanged(ISettingDefinition[] settings)
    {
        if (settings.Any(setting =>
            setting.Key == CaptureToolSettings.Settings_AiConsent_TextExtraction.Key ||
            setting.Key == CaptureToolSettings.Settings_AiConsent_ImageSuperResolution.Key ||
            setting.Key == CaptureToolSettings.Settings_AiConsent_ImageDescription.Key ||
            setting.Key == CaptureToolSettings.Settings_AiConsent_ImageForegroundExtraction.Key ||
            setting.Key == CaptureToolSettings.Settings_AiConsent_ImageObjectErase.Key ||
            setting.Key == CaptureToolSettings.Settings_AiConsent_ImageObjectExtraction.Key))
        {
            UpdateCanToggleSuperResolution();
            UpdateCanToggleTextExtraction();
            UpdateCanToggleImageDescription();
            UpdateCanToggleForegroundExtraction();
            UpdateCanToggleObjectErase();
            UpdateCanToggleObjectExtraction();
        }
    }

    public override async Task LoadAsync(ImageFile imageFile, CancellationToken cancellationToken)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();
        ClearImageDescriptionResults();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            Vector2 topLeft = Vector2.Zero;
            ImageFile = imageFile;
            _editSession = new ImageEditSession(_imageMetadataService.GetImageFileSize(imageFile));
            SyncImageGeometryFromSession();
            _originalImageFile = imageFile;
            _originalImageSize = ImageSize;
            ApplyImageSizeBasedDefaults(ImageSize);

            _imageDrawable = new(topLeft, imageFile, ImageSize);
            _editSession.AddDrawable(_imageDrawable);
            SyncDrawablesFromSession();

            UpdateUndoRedoStackProperties();
            await ChromaKeyTool.LoadAsync(imageFile, cancellationToken);

            InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(imageFile, cancellationToken);
        UpdateSuperResolutionAvailability();
        UpdateCanToggleSuperResolution();
        UpdateTextExtractionAvailability();
        UpdateCanToggleTextExtraction();
        UpdateImageDescriptionAvailability();
        UpdateCanToggleImageDescription();
        UpdateCanGenerateImageDescription();
        UpdateForegroundExtractionAvailability();
        UpdateCanToggleForegroundExtraction();
        UpdateObjectEraseAvailability();
        UpdateCanToggleObjectErase();
        UpdateObjectExtractionAvailability();
        UpdateCanToggleObjectExtraction();
        TrackEditorOpened();
    }

    public override void Dispose()
    {
        CancelSuperResolutionWork();
        CancelTextExtractionWork();
        CancelImageDescriptionWork();
        CancelForegroundExtractionWork();
        CancelObjectEraseWork();
        CancelObjectExtractionWork();
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
        ChromaKeyTool.SettingsChanged -= ChromaKeyTool_SettingsChanged;
        ChromaKeyTool.InteractionCommitted -= ChromaKeyTool_InteractionCommitted;
        _imageDrawable = null;
        _originalImageFile = null;
        _originalImageSize = Size.Empty;
        _superResolutionImageFile = null;
        _superResolutionImageSize = Size.Empty;
        IsSuperResolutionActive = false;
        IsSuperResolutionFeatureEnabled = _imageSuperResolutionFeatureAvailability.IsImageSuperResolutionEnabled;
        IsSuperResolutionAvailable = false;
        IsSuperResolutionGenerating = false;
        SuperResolutionStatusMessage = string.Empty;
        IsTextExtractionFeatureEnabled = _textExtractionFeatureAvailability.IsTextExtractionEnabled;
        IsTextExtractionAvailable = false;
        IsTextExtractionRunning = false;
        TextExtractionStatusMessage = string.Empty;
        TextExtractionRegions = [];
        TextExtractionTool.Reset();
        IsImageDescriptionFeatureEnabled = _imageDescriptionFeatureAvailability.IsImageDescriptionEnabled;
        IsImageDescriptionAvailable = false;
        IsImageDescriptionRunning = false;
        ClearImageDescriptionResults();
        IsForegroundExtractionFeatureEnabled = _imageForegroundExtractionFeatureAvailability.IsImageForegroundExtractionEnabled;
        IsForegroundExtractionAvailable = false;
        IsForegroundExtractionRunning = false;
        ForegroundExtractionStatusMessage = string.Empty;
        IsObjectEraseFeatureEnabled = _imageObjectEraseFeatureAvailability.IsImageObjectEraseEnabled;
        IsObjectEraseAvailable = false;
        IsObjectEraseRunning = false;
        ObjectEraseStatusMessage = string.Empty;
        IsObjectExtractionFeatureEnabled = _imageObjectExtractionFeatureAvailability.IsImageObjectExtractionEnabled;
        IsObjectExtractionAvailable = false;
        IsObjectExtractionRunning = false;
        ObjectExtractionStatusMessage = string.Empty;
        _editRevision = 0;
        _textExtractionProcessedRevision = null;
        _editHistory.Clear();
        HasUnsavedChanges = false;
        ApplyActiveMode(_modeStateMachine.Reset());
        _editSession = new ImageEditSession(Size.Empty, ImageOrientation.RotateNoneFlipNone, Rectangle.Empty);
        SyncImageGeometryFromSession();
        SyncDrawablesFromSession();
        ChromaKeyTool.Reset();
        ColorPickerTool.Reset();
        UpdateUndoRedoStackProperties();
        base.Dispose();
    }

    private async Task CopyAsync()
    {
        try
        {
            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            await _imageCanvasExporter.CopyImageToClipboardAsync([.. Drawables], options);
            TrackOutput("copy", TelemetryOutcomes.Succeeded);
        }
        catch
        {
            TrackOutput("copy", TelemetryOutcomes.Failed);
            throw;
        }
    }

    private async Task CopyImageDescriptionAsync()
    {
        if (!HasImageDescription)
        {
            return;
        }

        try
        {
            await _clipboardService.CopyTextAsync(ImageDescription);
            _notificationService.ShowInfo(GetLocalizedString(ImageDescriptionCopiedMessageResourceKey));
            TrackOutput("copy_image_description", TelemetryOutcomes.Succeeded);
        }
        catch (Exception)
        {
            _notificationService.ShowError(GetLocalizedString(ImageDescriptionCopyFailedMessageResourceKey));
            TrackOutput("copy_image_description", TelemetryOutcomes.Failed);
        }
    }

    private IAsyncRelayCommand CreateImageDescriptionCommand(ImageDescriptionMode mode)
    {
        return new AsyncRelayCommand(
            () => GenerateImageDescriptionAsync(mode),
            () => CanGenerateImageDescription && _runningImageDescriptionMode != mode,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler |
                AsyncRelayCommandOptions.AllowConcurrentExecutions);
    }

    private async Task ToggleSuperResolutionAsync()
    {
        if (!IsSuperResolutionFeatureEnabled)
        {
            return;
        }

        if (IsSuperResolutionActive)
        {
            RestoreOriginalImageVariant();
            return;
        }

        if (!await EnsureAiFeatureConsentAsync(AiFeatureId.ImageSuperResolution, CancellationToken.None))
        {
            TrackEditTool("super_resolution", TelemetryOutcomes.Canceled);
            UpdateCanToggleSuperResolution();
            return;
        }

        await ShowSuperResolutionImageAsync();
    }

    private async Task ToggleTextExtractionModeAsync()
    {
        if (!IsTextExtractionFeatureEnabled || !IsTextExtractionAvailable)
        {
            return;
        }

        if (IsTextExtractionModeActive)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.TextExtraction));
            return;
        }

        if (!await EnsureAiFeatureConsentAsync(AiFeatureId.TextExtraction, CancellationToken.None))
        {
            TrackEditTool("text_extraction", TelemetryOutcomes.Canceled);
            RefreshTextExtractionToggleState();
            UpdateCanToggleTextExtraction();
            return;
        }

        ApplyActiveMode(_modeStateMachine.Activate(ImageEditMode.TextExtraction));
        await EnsureTextExtractionCurrentAsync();
    }

    private async Task ToggleImageDescriptionModeAsync()
    {
        if (!IsImageDescriptionFeatureEnabled || !IsImageDescriptionAvailable)
        {
            return;
        }

        if (IsImageDescriptionModeActive)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.ImageDescription));
            return;
        }

        if (!await EnsureAiFeatureConsentAsync(AiFeatureId.ImageDescription, CancellationToken.None))
        {
            TrackEditTool("image_description", TelemetryOutcomes.Canceled);
            RefreshImageDescriptionToggleState();
            UpdateCanToggleImageDescription();
            return;
        }

        ApplyActiveMode(_modeStateMachine.Activate(ImageEditMode.ImageDescription));
    }

    private async Task ToggleForegroundExtractionModeAsync()
    {
        if (!IsForegroundExtractionFeatureEnabled || !IsForegroundExtractionAvailable)
        {
            return;
        }

        if (IsForegroundExtractionModeActive)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.ForegroundExtraction));
            return;
        }

        if (!await EnsureAiFeatureConsentAsync(AiFeatureId.ImageForegroundExtraction, CancellationToken.None))
        {
            TrackEditTool("foreground_extraction", TelemetryOutcomes.Canceled);
            RefreshForegroundExtractionToggleState();
            UpdateCanToggleForegroundExtraction();
            return;
        }

        ForegroundExtractionStatusMessage = string.Empty;
        ApplyActiveMode(_modeStateMachine.Activate(ImageEditMode.ForegroundExtraction));
    }

    private async Task ToggleObjectEraseModeAsync()
    {
        if (!IsObjectEraseFeatureEnabled || !IsObjectEraseAvailable)
        {
            return;
        }

        if (IsObjectEraseModeActive)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.ObjectErase));
            return;
        }

        if (!await EnsureAiFeatureConsentAsync(AiFeatureId.ImageObjectErase, CancellationToken.None))
        {
            TrackEditTool("object_erase", TelemetryOutcomes.Canceled);
            RefreshObjectEraseToggleState();
            UpdateCanToggleObjectErase();
            return;
        }

        ObjectEraseStatusMessage = string.Empty;
        ApplyActiveMode(_modeStateMachine.Activate(ImageEditMode.ObjectErase));
    }

    private async Task ToggleObjectExtractionModeAsync()
    {
        if (!IsObjectExtractionFeatureEnabled || !IsObjectExtractionAvailable)
        {
            return;
        }

        if (IsObjectExtractionModeActive)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.ObjectExtraction));
            return;
        }

        if (!await EnsureAiFeatureConsentAsync(AiFeatureId.ImageObjectExtraction, CancellationToken.None))
        {
            TrackEditTool("object_extraction", TelemetryOutcomes.Canceled);
            RefreshObjectExtractionToggleState();
            UpdateCanToggleObjectExtraction();
            return;
        }

        ObjectExtractionStatusMessage = string.Empty;
        ApplyActiveMode(_modeStateMachine.Activate(ImageEditMode.ObjectExtraction));
    }

    private void SetChromaKeyModeActive(bool value)
    {
        ApplyActiveMode(value
            ? _modeStateMachine.Activate(ImageEditMode.ChromaKey)
            : _modeStateMachine.Deactivate(ImageEditMode.ChromaKey));
    }

    private void ToggleCropMode()
    {
        ApplyActiveMode(_modeStateMachine.Toggle(ImageEditMode.Crop));
    }

    private void ToggleShapesMode()
    {
        ApplyActiveMode(_modeStateMachine.Toggle(ImageEditMode.Shapes));
    }

    private void ToggleTextMode()
    {
        ApplyActiveMode(_modeStateMachine.Toggle(ImageEditMode.Text));
    }

    private void ToggleColorPickerMode()
    {
        ApplyActiveMode(_modeStateMachine.Toggle(ImageEditMode.ColorPicker));
    }

    private void ApplyActiveMode(ImageEditMode mode)
    {
        bool wasTextExtractionModeActive = IsTextExtractionModeActive;
        bool wasImageDescriptionModeActive = IsImageDescriptionModeActive;
        bool wasForegroundExtractionModeActive = IsForegroundExtractionModeActive;
        bool wasObjectEraseModeActive = IsObjectEraseModeActive;
        bool wasObjectExtractionModeActive = IsObjectExtractionModeActive;
        IsCropModeActive = mode == ImageEditMode.Crop;
        IsShapesModeActive = mode == ImageEditMode.Shapes;
        IsTextModeActive = mode == ImageEditMode.Text;
        IsChromaKeyModeActive = mode == ImageEditMode.ChromaKey;
        IsColorPickerModeActive = mode == ImageEditMode.ColorPicker;
        IsTextExtractionModeActive = mode == ImageEditMode.TextExtraction;
        IsImageDescriptionModeActive = mode == ImageEditMode.ImageDescription;
        IsForegroundExtractionModeActive = mode == ImageEditMode.ForegroundExtraction;
        IsObjectEraseModeActive = mode == ImageEditMode.ObjectErase;
        IsObjectExtractionModeActive = mode == ImageEditMode.ObjectExtraction;

        if (wasTextExtractionModeActive && !IsTextExtractionModeActive)
        {
            CancelTextExtractionWork();
            TextExtractionRegions = [];
            TextExtractionStatusMessage = string.Empty;
            TextExtractionTool.Reset();
            _textExtractionProcessedRevision = null;
            InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
        }

        if (wasImageDescriptionModeActive && !IsImageDescriptionModeActive)
        {
            CancelImageDescriptionWork();
            ClearDisplayedImageDescription();
        }

        if (wasForegroundExtractionModeActive && !IsForegroundExtractionModeActive)
        {
            CancelForegroundExtractionWork();
            ForegroundExtractionStatusMessage = string.Empty;
        }

        if (wasObjectEraseModeActive && !IsObjectEraseModeActive)
        {
            CancelObjectEraseWork();
            ObjectEraseStatusMessage = string.Empty;
        }

        if (wasObjectExtractionModeActive && !IsObjectExtractionModeActive)
        {
            CancelObjectExtractionWork();
            ObjectExtractionStatusMessage = string.Empty;
        }

        UpdateCanToggleImageDescription();
        UpdateCanGenerateImageDescription();
        UpdateCanToggleForegroundExtraction();
        UpdateCanToggleObjectErase();
        UpdateCanToggleObjectExtraction();
    }

    private void ApplyImageSizeBasedDefaults(Size imageSize)
    {
        ShapeTool.ApplyImageSizeDefaults(imageSize);
        TextTool.ApplyImageSizeDefaults(imageSize);
    }

    public void OnShapeDrawn(Vector2 startPoint, Vector2 endPoint)
    {
        if (!IsShapesModeActive)
        {
            return;
        }

        IDrawable? newShape = ShapeTool.CreateDrawable(startPoint, endPoint);

        if (newShape != null)
        {
            ExecuteEditCommand(new AddDrawableCommand(newShape), CanvasUpdateMode.Redraw);
            TrackEditTool("shape");
        }
    }

    public void OnTextBoxDrawn(Vector2 startPoint, Vector2 endPoint)
    {
        if (!IsTextModeActive)
        {
            return;
        }

        TextDrawable? newText = TextTool.CreateDrawable(startPoint, endPoint);

        if (newText != null)
        {
            ExecuteEditCommand(new AddDrawableCommand(newText), CanvasUpdateMode.Redraw);
            TrackEditTool("text");
        }
    }

    public void OnColorPickerColorHovered(Color color)
    {
        if (!IsColorPickerModeActive)
        {
            return;
        }

        ColorPickerTool.UpdatePickedColorCommand.Execute(color);
    }

    public async Task OnColorPickerColorPickedAsync(Color color)
    {
        if (!IsColorPickerModeActive)
        {
            return;
        }

        ColorPickerTool.UpdatePickedColorCommand.Execute(color);
        await ColorPickerTool.CopyPickedColorCommand.ExecuteAsync(null);
        TrackEditTool("color_picker");
    }

    public void OnShapeDeleted(int shapeIndex)
    {
        if (!IsShapesModeActive && !IsTextModeActive)
        {
            return;
        }

        if (shapeIndex >= 0 && shapeIndex < _editSession.Drawables.Count)
        {
            ExecuteEditCommand(new DeleteDrawableCommand(shapeIndex), CanvasUpdateMode.Redraw);
            TrackEditTool(IsTextModeActive ? "text_delete" : "shape_delete");
        }
    }

    public void OnShapeModified(int shapeIndex, ModifyShapeOperation.ShapeState oldState, ModifyShapeOperation.ShapeState newState)
    {
        if (!IsShapesModeActive && !IsTextModeActive)
        {
            return;
        }

        if (shapeIndex >= 0 && shapeIndex < _editSession.Drawables.Count)
        {
            ExecuteEditCommand(
                new ModifyDrawableCommand(shapeIndex, oldState, newState),
                CanvasUpdateMode.Redraw);
            TrackEditTool(IsTextModeActive ? "text_modify" : "shape_modify");
        }
    }

    /// <summary>
    /// Adds a drawable to the canvas. Primarily for testing purposes.
    /// </summary>
    public void AddDrawable(IDrawable drawable)
    {
        if (drawable == null)
        {
            return;
        }

        _editSession.AddDrawable(drawable);
        SyncDrawablesFromSession();
        IncrementEditRevision();
        RequestCanvasUpdate(CanvasUpdateMode.Redraw);
    }

    private void UpdateChromaKeyEffectValues()
    {
        _editSession.SetChromaKeySettings(ChromaKeyTool.CaptureSettings());
        SyncDrawablesFromSession();

        RequestCanvasUpdate(CanvasUpdateMode.Redraw);
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            FileReference? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Image, UserFolder.Pictures);

            if (file is null)
            {
                TrackOutput("save", TelemetryOutcomes.Canceled);
                return false;
            }

            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            await _imageCanvasExporter.SaveImageAsync(file.FilePath, [.. Drawables], options);
            HasUnsavedChanges = false;
            _hasUnsavedChangesBeforeSuperResolution = false;
            _hasUserEditsSinceSuperResolutionActivated = false;
            TrackOutput("save", TelemetryOutcomes.Succeeded);
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to save image edits.");
            TrackOutput("save", TelemetryOutcomes.Failed);
            return false;
        }
    }

    private async Task SaveCommandAsync()
    {
        await SaveAsync(CancellationToken.None);
    }

    private async Task OpenScreenshotsFolderAsync()
    {
        await _openScreenshotsFolderAction.ExecuteAsync(new OpenScreenshotsFolderRequest(), CancellationToken.None);
    }

    private ImageCanvasRenderOptions GetImageCanvasRenderOptions()
    {
        ImageEditRenderSnapshot snapshot = _editSession.CreateRenderSnapshot();
        return new(snapshot.Orientation, snapshot.ImageSize, snapshot.CropRect);
    }

    private void Undo()
    {
        string? previousImagePath = _imageDrawable?.File.FilePath;
        ImageEditRenderSnapshot previousRenderSnapshot = _editSession.CreateRenderSnapshot();
        if (!_editHistory.Undo(_editSession))
        {
            return;
        }

        SyncImageGeometryFromSession();
        SyncDrawablesFromSession();
        SyncImageFileFromDrawable();
        SyncChromaKeySettingsFromSession();
        UpdateUndoRedoStackProperties();
        IncrementEditRevision();
        HasUnsavedChanges = true;
        RequestCanvasUpdateAfterHistory(previousImagePath, previousRenderSnapshot);
        TrackEditTool("undo");
    }

    private void Redo()
    {
        string? previousImagePath = _imageDrawable?.File.FilePath;
        ImageEditRenderSnapshot previousRenderSnapshot = _editSession.CreateRenderSnapshot();
        if (!_editHistory.Redo(_editSession))
        {
            return;
        }

        SyncImageGeometryFromSession();
        SyncDrawablesFromSession();
        SyncImageFileFromDrawable();
        SyncChromaKeySettingsFromSession();
        UpdateUndoRedoStackProperties();
        IncrementEditRevision();
        HasUnsavedChanges = true;
        RequestCanvasUpdateAfterHistory(previousImagePath, previousRenderSnapshot);
        TrackEditTool("redo");
    }

    private void Rotate()
    {
        ExecuteEditCommand(new RotateImageCommand(RotationDirection.Clockwise));
    }

    private void Flip(FlipDirection flipDirection)
    {
        ExecuteEditCommand(new FlipImageCommand(flipDirection));
    }

    private void UpdateUndoRedoStackProperties()
    {
        HasUndoStack = _editHistory.CanUndo;
        HasRedoStack = _editHistory.CanRedo;
    }

    private async Task PrintAsync()
    {
        try
        {
            await _imageCanvasPrinter.ShowPrintUIAsync([.. Drawables], GetImageCanvasRenderOptions());
            TrackOutput("print", TelemetryOutcomes.Succeeded);
        }
        catch
        {
            TrackOutput("print", TelemetryOutcomes.Failed);
            throw;
        }
    }

    private async Task ShareAsync()
    {
        if (ImageFile == null)
        {
            return;
        }

        try
        {
            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            using MemoryStream renderedStream = await _imageCanvasExporter.RenderToStreamAsync([.. Drawables], options);
            await _shareService.ShareStreamAsync(renderedStream);
            TrackOutput("share", TelemetryOutcomes.Succeeded);
        }
        catch
        {
            TrackOutput("share", TelemetryOutcomes.Failed);
            throw;
        }
    }

    private async Task EditInPaintAsync()
    {
        if (!IsLoaded)
        {
            return;
        }

        string imagePath = GetTemporaryPaintImagePath();
        await _imageCanvasExporter.SaveImageAsync(imagePath, [.. Drawables], GetImageCanvasRenderOptions());
        var response = await _openExternalEditorAction.ExecuteAsync(
            new OpenExternalEditorRequest(imagePath, ExternalMediaEditor.Paint),
            CancellationToken.None);
        TrackOutput(
            "open_external_editor",
            response?.Result == UseCaseResult.Cancelled
                ? TelemetryOutcomes.Canceled
                : response?.Result == UseCaseResult.Succeeded &&
                    response.Value?.Opened == true
                    ? TelemetryOutcomes.Succeeded
                    : TelemetryOutcomes.Failed);
    }

    private string GetTemporaryPaintImagePath()
    {
        return Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            $"{Path.GetFileNameWithoutExtension(_storageService.GetTemporaryFileName())}.png");
    }

    public void OnCropInteractionComplete(Rectangle oldCropRect)
    {
        Rectangle newCropRect = CropRect;
        if (newCropRect != oldCropRect)
        {
            ExecuteEditCommand(new SetCropCommand(oldCropRect, newCropRect));
        }
    }

    private void UpdateOrientation(ImageOrientation newOrientation)
    {
        _editSession.SetOrientation(newOrientation);
        SyncImageGeometryFromSession();
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCropRect(Rectangle newCropRect)
    {
        _editSession.SetCropRect(newCropRect);
        SyncImageGeometryFromSession();
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteEditCommand(
        IImageEditCommand command,
        CanvasUpdateMode canvasUpdateMode = CanvasUpdateMode.InvalidateLayout,
        bool preservePointSelectionAiMode = false)
    {
        _editHistory.Execute(_editSession, command);
        SyncImageGeometryFromSession();
        SyncDrawablesFromSession();
        SyncImageFileFromDrawable();
        SyncChromaKeySettingsFromSession();
        UpdateUndoRedoStackProperties();
        IncrementEditRevision(preservePointSelectionAiMode);
        MarkUnsavedChanges();
        RequestCanvasUpdate(canvasUpdateMode);
        string? telemetryTool = GetTelemetryTool(command);
        if (telemetryTool is not null)
        {
            TrackEditTool(telemetryTool);
        }
    }

    private void TrackEditorOpened()
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.EditorOpened,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.MediaType] = "image"
            });
    }

    private void TrackEditTool(string tool, string outcome = TelemetryOutcomes.Succeeded)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.EditToolInvoked,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Tool] = tool,
                [TelemetryProperties.MediaType] = "image",
                [TelemetryProperties.Outcome] = outcome
            });
    }

    private void TrackOutput(string operation, string outcome)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.OutputCompleted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Operation] = operation,
                [TelemetryProperties.MediaType] = "image",
                [TelemetryProperties.Outcome] = outcome,
                [TelemetryProperties.Source] = "image_editor"
            });
    }

    private static string? GetTelemetryTool(IImageEditCommand command)
    {
        return command switch
        {
            SetCropCommand => "crop",
            RotateImageCommand => "rotate",
            FlipImageCommand => "flip",
            SetChromaKeyCommand => "chroma_key",
            AddDrawableCommand => null,
            ModifyDrawableCommand => null,
            DeleteDrawableCommand => null,
            ReplaceImageDrawableFileCommand => null,
            _ => "other"
        };
    }

    private static string GetImageDescriptionTelemetryTool(ImageDescriptionMode mode)
    {
        return $"image_description_{mode.ToString().ToLowerInvariant()}";
    }

    private void SyncImageGeometryFromSession()
    {
        ImageSize = _editSession.ImageSize;
        CropRect = _editSession.CropRect;
        Orientation = _editSession.Orientation;
        MirroredDisplayName = GetMirroredDisplayName(Orientation);
        RotationDisplayName = GetRotationDisplayName(Orientation);
    }

    private void SyncDrawablesFromSession()
    {
        Drawables = _editSession.Drawables;
        _imageDrawable = Drawables.OfType<ImageDrawable>().FirstOrDefault();
    }

    private void SyncImageFileFromDrawable()
    {
        if (_imageDrawable is not null)
        {
            ImageFile = _imageDrawable.File;
        }
    }

    private void RequestCanvasUpdateAfterHistory(
        string? previousImagePath,
        ImageEditRenderSnapshot previousRenderSnapshot)
    {
        if (!string.Equals(previousImagePath, _imageDrawable?.File.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            RequestCanvasUpdate(CanvasUpdateMode.ReloadResources);
        }
        else if (previousRenderSnapshot != _editSession.CreateRenderSnapshot())
        {
            RequestCanvasUpdate(CanvasUpdateMode.InvalidateLayout);
        }
        else
        {
            RequestCanvasUpdate(CanvasUpdateMode.Redraw);
        }
    }

    private void RequestCanvasUpdate(CanvasUpdateMode mode)
    {
        switch (mode)
        {
            case CanvasUpdateMode.InvalidateLayout:
                InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
                break;

            case CanvasUpdateMode.Redraw:
                RedrawCanvasRequested?.Invoke(this, EventArgs.Empty);
                break;

            case CanvasUpdateMode.ReloadResources:
                ReloadCanvasResourcesRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void SyncChromaKeySettingsFromSession()
    {
        ChromaKeyTool.ApplySettings(_editSession.ChromaKeySettings);
    }

    private string GetMirroredDisplayName(ImageOrientation orientation)
    {
        if (IsMirrored(orientation))
        {
            return _localizationService.GetString($"{nameof(ImageOrientation)}_Mirrored");
        }
        else
        {
            return _localizationService.GetString($"{nameof(ImageOrientation)}_Normal");
        }
    }

    private static string GetRotationDisplayName(ImageOrientation orientation)
    {
        return orientation switch
        {
            ImageOrientation.RotateNoneFlipNone or ImageOrientation.RotateNoneFlipX => "0°",
            ImageOrientation.Rotate90FlipNone or ImageOrientation.Rotate90FlipX => "90°",
            ImageOrientation.Rotate180FlipNone or ImageOrientation.Rotate180FlipX => "180°",
            ImageOrientation.Rotate270FlipNone or ImageOrientation.Rotate270FlipX => "270°",
            _ => string.Empty,
        };
    }

    private static bool IsMirrored(ImageOrientation orientation)
    {
        return orientation switch
        {
            ImageOrientation.RotateNoneFlipX or
            ImageOrientation.Rotate90FlipX or
            ImageOrientation.Rotate180FlipX or
            ImageOrientation.Rotate270FlipX => true,
            _ => false,
        };
    }

    private void UpdateZoomPercentage(int percentage)
    {
        ZoomPercentage = Math.Clamp(percentage, 1, 200);
    }

    private void UpdateAutoZoomLock(bool isLocked)
    {
        IsAutoZoomLocked = isLocked;
        if (!isLocked)
        {
            // When unlocking, trigger a zoom and center
            RequestZoomAndCenter();
        }
    }

    private void RequestZoomAndCenter()
    {
        ForceZoomAndCenterRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task OnForegroundExtractionRequestedAsync(Vector2 foregroundPoint)
    {
        if (!IsLoaded ||
            !IsForegroundExtractionModeActive ||
            IsForegroundExtractionRunning ||
            _imageDrawable is null ||
            foregroundPoint.X < 0 ||
            foregroundPoint.Y < 0 ||
            foregroundPoint.X >= _imageDrawable.ImageSize.Width ||
            foregroundPoint.Y >= _imageDrawable.ImageSize.Height)
        {
            return;
        }

        CancelForegroundExtractionWork();
        ForegroundExtractionStatusMessage = string.Empty;

        var cancellationTokenSource = new CancellationTokenSource();
        _foregroundExtractionCancellationTokenSource = cancellationTokenSource;
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        ImageFile sourceImage = _imageDrawable.File;
        Size sourceSize = _imageDrawable.ImageSize;
        IsForegroundExtractionRunning = true;

        try
        {
            ForegroundExtractionReadyState readyState = _imageForegroundExtractionService.GetReadyState();
            if (readyState == ForegroundExtractionReadyState.PreparationNeeded)
            {
                ForegroundExtractionPreparationResult preparationResult =
                    await _imageForegroundExtractionService.EnsureReadyAsync(cancellationToken);
                if (preparationResult.Status != ForegroundExtractionPreparationStatus.Success)
                {
                    ShowForegroundExtractionFailure(GetForegroundExtractionPreparationFailureMessage(preparationResult));
                    UpdateForegroundExtractionAvailability();
                    return;
                }
            }
            else if (readyState != ForegroundExtractionReadyState.Ready)
            {
                ShowForegroundExtractionFailure(GetForegroundExtractionReadyStateFailureMessage(readyState));
                UpdateForegroundExtractionAvailability();
                return;
            }

            ForegroundExtractionResult result = await _imageForegroundExtractionService.ExtractAsync(
                new ForegroundExtractionRequest(
                    sourceImage,
                    sourceSize,
                    new Point(
                        (int)Math.Round(foregroundPoint.X),
                        (int)Math.Round(foregroundPoint.Y))),
                cancellationToken);

            if (result.Status != ForegroundExtractionStatus.Success || result.ImageFile is null)
            {
                ShowForegroundExtractionFailure(GetForegroundExtractionFailureMessage(result));
                return;
            }

            if (cancellationToken.IsCancellationRequested ||
                !ReferenceEquals(_foregroundExtractionCancellationTokenSource, cancellationTokenSource) ||
                !IsForegroundExtractionModeActive ||
                _imageDrawable is null ||
                !string.Equals(_imageDrawable.File.FilePath, sourceImage.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int drawableIndex = -1;
            for (var index = 0; index < _editSession.Drawables.Count; index++)
            {
                if (ReferenceEquals(_editSession.Drawables[index], _imageDrawable))
                {
                    drawableIndex = index;
                    break;
                }
            }

            if (drawableIndex < 0)
            {
                return;
            }

            ExecuteEditCommand(
                new ReplaceImageDrawableFileCommand(drawableIndex, sourceImage, result.ImageFile),
                CanvasUpdateMode.ReloadResources,
                preservePointSelectionAiMode: true);
            ForegroundExtractionStatusMessage = GetLocalizedString("ForegroundExtractionStatus_Success");
            TrackEditTool("foreground_extraction");
        }
        catch (OperationCanceledException)
        {
            TrackEditTool("foreground_extraction", TelemetryOutcomes.Canceled);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to extract the image foreground.");
            ShowForegroundExtractionFailure(GetLocalizedString("ForegroundExtractionStatus_Failed"));
        }
        finally
        {
            if (ReferenceEquals(_foregroundExtractionCancellationTokenSource, cancellationTokenSource))
            {
                _foregroundExtractionCancellationTokenSource = null;
                IsForegroundExtractionRunning = false;
            }

            cancellationTokenSource.Dispose();
            UpdateCanToggleForegroundExtraction();
        }
    }

    public async Task OnObjectEraseRequestedAsync(Vector2 objectPoint)
    {
        if (!IsLoaded ||
            !IsObjectEraseModeActive ||
            IsObjectEraseRunning ||
            _imageDrawable is null ||
            objectPoint.X < 0 ||
            objectPoint.Y < 0 ||
            objectPoint.X >= _imageDrawable.ImageSize.Width ||
            objectPoint.Y >= _imageDrawable.ImageSize.Height)
        {
            return;
        }

        CancelObjectEraseWork();
        ObjectEraseStatusMessage = string.Empty;

        var cancellationTokenSource = new CancellationTokenSource();
        _objectEraseCancellationTokenSource = cancellationTokenSource;
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        ImageFile sourceImage = _imageDrawable.File;
        Size sourceSize = _imageDrawable.ImageSize;
        IsObjectEraseRunning = true;

        try
        {
            ObjectEraseReadyState readyState = _imageObjectEraseService.GetReadyState();
            if (readyState == ObjectEraseReadyState.PreparationNeeded)
            {
                ObjectErasePreparationResult preparationResult =
                    await _imageObjectEraseService.EnsureReadyAsync(cancellationToken);
                if (preparationResult.Status != ObjectErasePreparationStatus.Success)
                {
                    ShowObjectEraseFailure(GetObjectErasePreparationFailureMessage(preparationResult));
                    UpdateObjectEraseAvailability();
                    return;
                }
            }
            else if (readyState != ObjectEraseReadyState.Ready)
            {
                ShowObjectEraseFailure(GetObjectEraseReadyStateFailureMessage(readyState));
                UpdateObjectEraseAvailability();
                return;
            }

            ObjectEraseResult result = await _imageObjectEraseService.EraseAsync(
                new ObjectEraseRequest(
                    sourceImage,
                    sourceSize,
                    new Point(
                        (int)Math.Round(objectPoint.X),
                        (int)Math.Round(objectPoint.Y))),
                cancellationToken);

            if (result.Status != ObjectEraseStatus.Success || result.ImageFile is null)
            {
                ShowObjectEraseFailure(GetObjectEraseFailureMessage(result));
                return;
            }

            if (cancellationToken.IsCancellationRequested ||
                !ReferenceEquals(_objectEraseCancellationTokenSource, cancellationTokenSource) ||
                !IsObjectEraseModeActive ||
                _imageDrawable is null ||
                !string.Equals(_imageDrawable.File.FilePath, sourceImage.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int drawableIndex = -1;
            for (var index = 0; index < _editSession.Drawables.Count; index++)
            {
                if (ReferenceEquals(_editSession.Drawables[index], _imageDrawable))
                {
                    drawableIndex = index;
                    break;
                }
            }

            if (drawableIndex < 0)
            {
                return;
            }

            ExecuteEditCommand(
                new ReplaceImageDrawableFileCommand(drawableIndex, sourceImage, result.ImageFile),
                CanvasUpdateMode.ReloadResources,
                preservePointSelectionAiMode: true);
            ObjectEraseStatusMessage = GetLocalizedString("ObjectEraseStatus_Success");
            TrackEditTool("object_erase");
        }
        catch (OperationCanceledException)
        {
            TrackEditTool("object_erase", TelemetryOutcomes.Canceled);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to erase the selected image object.");
            ShowObjectEraseFailure(GetLocalizedString("ObjectEraseStatus_Failed"));
        }
        finally
        {
            if (ReferenceEquals(_objectEraseCancellationTokenSource, cancellationTokenSource))
            {
                _objectEraseCancellationTokenSource = null;
                IsObjectEraseRunning = false;
            }

            cancellationTokenSource.Dispose();
            UpdateCanToggleObjectErase();
        }
    }

    public async Task OnObjectExtractionRequestedAsync(Vector2 objectPoint)
    {
        if (!IsLoaded ||
            !IsObjectExtractionModeActive ||
            IsObjectExtractionRunning ||
            _imageDrawable is null ||
            objectPoint.X < 0 ||
            objectPoint.Y < 0 ||
            objectPoint.X >= _imageDrawable.ImageSize.Width ||
            objectPoint.Y >= _imageDrawable.ImageSize.Height)
        {
            return;
        }

        CancelObjectExtractionWork();
        ObjectExtractionStatusMessage = string.Empty;

        var cancellationTokenSource = new CancellationTokenSource();
        _objectExtractionCancellationTokenSource = cancellationTokenSource;
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        ImageFile sourceImage = _imageDrawable.File;
        Size sourceSize = _imageDrawable.ImageSize;
        IsObjectExtractionRunning = true;

        try
        {
            ForegroundExtractionReadyState readyState = _imageForegroundExtractionService.GetReadyState();
            if (readyState == ForegroundExtractionReadyState.PreparationNeeded)
            {
                ForegroundExtractionPreparationResult preparationResult =
                    await _imageForegroundExtractionService.EnsureReadyAsync(cancellationToken);
                if (preparationResult.Status != ForegroundExtractionPreparationStatus.Success)
                {
                    ShowObjectExtractionFailure(GetObjectExtractionPreparationFailureMessage(preparationResult));
                    UpdateObjectExtractionAvailability();
                    return;
                }
            }
            else if (readyState != ForegroundExtractionReadyState.Ready)
            {
                ShowObjectExtractionFailure(GetObjectExtractionReadyStateFailureMessage(readyState));
                UpdateObjectExtractionAvailability();
                return;
            }

            ForegroundExtractionResult result = await _imageForegroundExtractionService.ExtractAsync(
                new ForegroundExtractionRequest(
                    sourceImage,
                    sourceSize,
                    new Point(
                        (int)Math.Round(objectPoint.X),
                        (int)Math.Round(objectPoint.Y))),
                cancellationToken);

            if (result.Status != ForegroundExtractionStatus.Success || result.ImageFile is null)
            {
                ShowObjectExtractionFailure(GetObjectExtractionFailureMessage(result));
                return;
            }

            if (cancellationToken.IsCancellationRequested ||
                !ReferenceEquals(_objectExtractionCancellationTokenSource, cancellationTokenSource) ||
                !IsObjectExtractionModeActive ||
                _imageDrawable is null ||
                !string.Equals(_imageDrawable.File.FilePath, sourceImage.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int drawableIndex = -1;
            for (var index = 0; index < _editSession.Drawables.Count; index++)
            {
                if (ReferenceEquals(_editSession.Drawables[index], _imageDrawable))
                {
                    drawableIndex = index;
                    break;
                }
            }

            if (drawableIndex < 0)
            {
                return;
            }

            ExecuteEditCommand(
                new ReplaceImageDrawableFileCommand(drawableIndex, sourceImage, result.ImageFile),
                CanvasUpdateMode.ReloadResources,
                preservePointSelectionAiMode: true);
            ObjectExtractionStatusMessage = GetLocalizedString("ObjectExtractionStatus_Success");
            TrackEditTool("object_extraction");
        }
        catch (OperationCanceledException)
        {
            TrackEditTool("object_extraction", TelemetryOutcomes.Canceled);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to extract the selected image object.");
            ShowObjectExtractionFailure(GetLocalizedString("ObjectExtractionStatus_Failed"));
        }
        finally
        {
            if (ReferenceEquals(_objectExtractionCancellationTokenSource, cancellationTokenSource))
            {
                _objectExtractionCancellationTokenSource = null;
                IsObjectExtractionRunning = false;
            }

            cancellationTokenSource.Dispose();
            UpdateCanToggleObjectExtraction();
        }
    }

    private async Task<bool> EnsureAiFeatureConsentAsync(AiFeatureId featureId, CancellationToken cancellationToken)
    {
        AiFeatureConsentState consentState = _aiFeatureConsentService.GetConsentState(featureId);
        if (consentState == AiFeatureConsentState.Granted)
        {
            return true;
        }

        bool consented = await _aiFeatureConsentDialogService.RequestConsentAsync(featureId, cancellationToken);
        await _aiFeatureConsentService.SetConsentAsync(featureId, consented, cancellationToken);
        UpdateCanToggleSuperResolution();
        UpdateCanToggleTextExtraction();
        UpdateCanToggleImageDescription();
        UpdateCanToggleForegroundExtraction();
        UpdateCanToggleObjectErase();
        UpdateCanToggleObjectExtraction();
        return consented;
    }

    private async Task EnsureTextExtractionCurrentAsync()
    {
        if (_textExtractionProcessedRevision == _editRevision)
        {
            return;
        }

        await RunTextExtractionAsync();
    }

    private async Task RunTextExtractionAsync()
    {
        if (!IsLoaded)
        {
            return;
        }

        TextExtractionStatusMessage = string.Empty;
        TextExtractionRegions = [];
        TextExtractionTool.Reset();

        _textExtractionCancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = _textExtractionCancellationTokenSource.Token;
        int processedRevision = _editRevision;
        IsTextExtractionRunning = true;

        try
        {
            TextExtractionReadyState readyState = _textExtractionService.GetReadyState();
            if (readyState == TextExtractionReadyState.PreparationNeeded)
            {
                TextExtractionPreparationResult preparationResult =
                    await _textExtractionService.EnsureReadyAsync(cancellationToken);
                if (preparationResult.Status != TextExtractionPreparationStatus.Success)
                {
                    ShowTextExtractionFailure(GetPreparationFailureMessage(preparationResult));
                    UpdateTextExtractionAvailability();
                    return;
                }
            }
            else if (readyState != TextExtractionReadyState.Ready)
            {
                ShowTextExtractionFailure(GetReadyStateFailureMessage(readyState));
                UpdateTextExtractionAvailability();
                return;
            }

            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            using MemoryStream sourceImage = await _imageCanvasExporter.RenderToStreamAsync([.. Drawables], options);
            cancellationToken.ThrowIfCancellationRequested();

            Size renderedSize = GetRenderedImageSize(options);
            TextExtractionResult result = await _textExtractionService.ExtractAsync(
                new TextExtractionRequest(sourceImage, renderedSize),
                cancellationToken);

            if (result.Status != TextExtractionStatus.Success || result.Document is null)
            {
                ShowTextExtractionFailure(GetExtractionFailureMessage(result));
                return;
            }

            TextExtractionRegions = NormalizeTextExtractionRegions(result.Document.Regions, result.Document.ImageSize);
            TextExtractionTool.SetText(result.Document.Text);
            _textExtractionProcessedRevision = processedRevision;
            InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
            TrackEditTool("text_extraction");
        }
        catch (OperationCanceledException)
        {
            TextExtractionStatusMessage = string.Empty;
            TrackEditTool("text_extraction", TelemetryOutcomes.Canceled);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to extract text from image.");
            ShowTextExtractionFailure(GetLocalizedString("TextExtractionStatus_Failed"));
        }
        finally
        {
            _textExtractionCancellationTokenSource?.Dispose();
            _textExtractionCancellationTokenSource = null;
            IsTextExtractionRunning = false;
            UpdateCanToggleTextExtraction();
        }
    }

    private static Size GetRenderedImageSize(ImageCanvasRenderOptions options)
    {
        return options.CropRect.Width > 0 && options.CropRect.Height > 0
            ? new Size(options.CropRect.Width, options.CropRect.Height)
            : options.CanvasSize;
    }

    private static IReadOnlyList<RecognizedTextRegion> NormalizeTextExtractionRegions(
        IReadOnlyList<RecognizedTextRegion> regions,
        Size imageSize)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            return [];
        }

        List<RecognizedTextRegion> normalizedRegions = [];
        RectangleF imageBounds = new(0, 0, imageSize.Width, imageSize.Height);
        foreach (RecognizedTextRegion region in regions)
        {
            RectangleF bounds = region.Bounds;
            float horizontalPadding = Math.Clamp(bounds.Height * 0.15f, 2, 8);
            float verticalPadding = Math.Clamp(bounds.Height * 0.1f, 2, 6);
            bounds.Inflate(horizontalPadding, verticalPadding);
            RectangleF clampedBounds = RectangleF.Intersect(imageBounds, bounds);
            if (clampedBounds.Width > 0 && clampedBounds.Height > 0)
            {
                normalizedRegions.Add(region with { Bounds = clampedBounds });
            }
        }

        return normalizedRegions;
    }

    private async Task GenerateImageDescriptionAsync(ImageDescriptionMode mode)
    {
        if (!CanGenerateImageDescription || _runningImageDescriptionMode == mode)
        {
            return;
        }

        CancelImageDescriptionWork();
        ClearDisplayedImageDescription();

        if (_imageDescriptionResults.TryGetValue(mode, out string? cachedDescription))
        {
            ShowImageDescription(mode, cachedDescription);
            TrackEditTool(GetImageDescriptionTelemetryTool(mode));
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        _imageDescriptionCancellationTokenSource = cancellationTokenSource;
        _runningImageDescriptionMode = mode;
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        IsImageDescriptionRunning = true;
        NotifyImageDescriptionCommandsCanExecuteChanged();
        RaiseImageDescriptionSelectionProperties();

        try
        {
            ImageDescriptionReadyState readyState = _imageDescriptionService.GetReadyState();
            if (readyState == ImageDescriptionReadyState.PreparationNeeded)
            {
                ImageDescriptionPreparationResult preparationResult =
                    await _imageDescriptionService.EnsureReadyAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_imageDescriptionCancellationTokenSource, cancellationTokenSource))
                {
                    return;
                }

                if (preparationResult.Status != ImageDescriptionPreparationStatus.Success)
                {
                    ShowImageDescriptionFailure(GetImageDescriptionPreparationFailureMessage(preparationResult));
                    UpdateImageDescriptionAvailability();
                    return;
                }
            }
            else if (readyState != ImageDescriptionReadyState.Ready)
            {
                ShowImageDescriptionFailure(GetImageDescriptionReadyStateFailureMessage(readyState));
                UpdateImageDescriptionAvailability();
                return;
            }

            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            using MemoryStream sourceImage = await _imageCanvasExporter.RenderToStreamAsync([.. Drawables], options);
            cancellationToken.ThrowIfCancellationRequested();

            ImageDescriptionResult result = await _imageDescriptionService.DescribeAsync(
                new ImageDescriptionRequest(sourceImage, GetRenderedImageSize(options), mode),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_imageDescriptionCancellationTokenSource, cancellationTokenSource))
            {
                return;
            }

            if (result.Status != ImageDescriptionStatus.Success)
            {
                ShowImageDescriptionFailure(GetImageDescriptionFailureMessage(result));
                return;
            }

            _imageDescriptionResults[mode] = result.Description;
            if (IsImageDescriptionModeActive)
            {
                ShowImageDescription(mode, result.Description);
            }
            TrackEditTool(GetImageDescriptionTelemetryTool(mode));
        }
        catch (OperationCanceledException)
        {
            TrackEditTool(GetImageDescriptionTelemetryTool(mode), TelemetryOutcomes.Canceled);
            if (ReferenceEquals(_imageDescriptionCancellationTokenSource, cancellationTokenSource))
            {
                ImageDescriptionStatusMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to describe image.");
            if (ReferenceEquals(_imageDescriptionCancellationTokenSource, cancellationTokenSource))
            {
                ShowImageDescriptionFailure(GetLocalizedString("ImageDescriptionStatus_Failed"));
            }
        }
        finally
        {
            if (ReferenceEquals(_imageDescriptionCancellationTokenSource, cancellationTokenSource))
            {
                _imageDescriptionCancellationTokenSource = null;
                _runningImageDescriptionMode = null;
                IsImageDescriptionRunning = false;
                NotifyImageDescriptionCommandsCanExecuteChanged();
            }

            cancellationTokenSource.Dispose();
            UpdateCanToggleImageDescription();
            UpdateCanGenerateImageDescription();
        }
    }

    private async Task ShowSuperResolutionImageAsync()
    {
        if (_originalImageFile is null || _imageDrawable is null)
        {
            return;
        }

        SuperResolutionStatusMessage = string.Empty;

        if (_superResolutionImageFile is not null)
        {
            ApplySuperResolutionImageVariant();
            return;
        }

        _superResolutionCancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = _superResolutionCancellationTokenSource.Token;
        IsSuperResolutionGenerating = true;

        try
        {
            ImageSuperResolutionReadyState readyState = _imageSuperResolutionService.GetReadyState();
            if (readyState == ImageSuperResolutionReadyState.PreparationNeeded)
            {
                bool consented = await _imageSuperResolutionPreparationConsentService.ConfirmPreparationAsync(cancellationToken);
                if (!consented)
                {
                    return;
                }

                ImageSuperResolutionPreparationResult preparationResult =
                    await _imageSuperResolutionService.EnsureReadyAsync(cancellationToken);
                if (preparationResult.Status != ImageSuperResolutionPreparationStatus.Success)
                {
                    ShowSuperResolutionFailure(GetPreparationFailureMessage(preparationResult));
                    UpdateSuperResolutionAvailability();
                    return;
                }
            }
            else if (readyState != ImageSuperResolutionReadyState.Ready)
            {
                ShowSuperResolutionFailure(GetReadyStateFailureMessage(readyState));
                UpdateSuperResolutionAvailability();
                return;
            }

            ImageSuperResolutionResult result = await _imageSuperResolutionService.GenerateAsync(
                new ImageSuperResolutionRequest(_originalImageFile, _originalImageSize),
                cancellationToken);

            if (result.Status != ImageSuperResolutionStatus.Success ||
                result.ImageFile is null ||
                result.ImageSize == Size.Empty)
            {
                ShowSuperResolutionFailure(GetGenerationFailureMessage(result));
                return;
            }

            _superResolutionImageFile = result.ImageFile;
            _superResolutionImageSize = result.ImageSize;
            ApplySuperResolutionImageVariant();
        }
        catch (OperationCanceledException)
        {
            SuperResolutionStatusMessage = string.Empty;
            TrackEditTool("super_resolution", TelemetryOutcomes.Canceled);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to generate super-resolution image.");
            ShowSuperResolutionFailure(GetLocalizedString("ImageSuperResolutionStatus_Failed"));
        }
        finally
        {
            _superResolutionCancellationTokenSource?.Dispose();
            _superResolutionCancellationTokenSource = null;
            IsSuperResolutionGenerating = false;
            RefreshSuperResolutionToggleState();
            UpdateCanToggleSuperResolution();
        }
    }

    private void RefreshSuperResolutionToggleState()
    {
        if (!IsSuperResolutionActive)
        {
            RaisePropertyChanged(nameof(IsSuperResolutionActive));
        }
    }

    private void RefreshTextExtractionToggleState()
    {
        if (!IsTextExtractionModeActive)
        {
            RaisePropertyChanged(nameof(IsTextExtractionModeActive));
        }
    }

    private void RefreshImageDescriptionToggleState()
    {
        if (!IsImageDescriptionModeActive)
        {
            RaisePropertyChanged(nameof(IsImageDescriptionModeActive));
        }
    }

    private void RefreshForegroundExtractionToggleState()
    {
        if (!IsForegroundExtractionModeActive)
        {
            RaisePropertyChanged(nameof(IsForegroundExtractionModeActive));
        }
    }

    private void RefreshObjectEraseToggleState()
    {
        if (!IsObjectEraseModeActive)
        {
            RaisePropertyChanged(nameof(IsObjectEraseModeActive));
        }
    }

    private void RefreshObjectExtractionToggleState()
    {
        if (!IsObjectExtractionModeActive)
        {
            RaisePropertyChanged(nameof(IsObjectExtractionModeActive));
        }
    }

    private void ApplySuperResolutionImageVariant()
    {
        if (_superResolutionImageFile is null || _superResolutionImageSize == Size.Empty)
        {
            return;
        }

        _hasUnsavedChangesBeforeSuperResolution = HasUnsavedChanges;
        _hasUserEditsSinceSuperResolutionActivated = false;
        ApplyImageVariant(_superResolutionImageFile, _superResolutionImageSize, true);
        HasUnsavedChanges = true;
        TrackEditTool("super_resolution");
    }

    private void RestoreOriginalImageVariant()
    {
        if (_originalImageFile is null || _originalImageSize == Size.Empty)
        {
            return;
        }

        bool shouldRemainDirty = _hasUserEditsSinceSuperResolutionActivated || _hasUnsavedChangesBeforeSuperResolution;
        ApplyImageVariant(_originalImageFile, _originalImageSize, false);
        HasUnsavedChanges = shouldRemainDirty;
        _hasUserEditsSinceSuperResolutionActivated = false;
        SuperResolutionStatusMessage = string.Empty;
    }

    private void ApplyImageVariant(ImageFile imageFile, Size imageSize, bool isSuperResolutionActive)
    {
        if (_imageDrawable is null)
        {
            return;
        }

        _editSession.ResizeImage(imageSize);
        _imageDrawable.File = imageFile;
        _imageDrawable.ImageSize = imageSize;
        ImageFile = imageFile;
        IsSuperResolutionActive = isSuperResolutionActive;
        IncrementEditRevision();
        SyncImageGeometryFromSession();
        SyncDrawablesFromSession();
        ReloadCanvasResourcesRequested?.Invoke(this, EventArgs.Empty);
        ForceZoomAndCenterRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MarkUnsavedChanges()
    {
        HasUnsavedChanges = true;
        if (IsSuperResolutionActive)
        {
            _hasUserEditsSinceSuperResolutionActivated = true;
        }
    }

    private void UpdateSuperResolutionAvailability()
    {
        IsSuperResolutionFeatureEnabled = _imageSuperResolutionFeatureAvailability.IsImageSuperResolutionEnabled;
        if (!IsSuperResolutionFeatureEnabled)
        {
            IsSuperResolutionAvailable = false;
            return;
        }

        ImageSuperResolutionReadyState readyState = _imageSuperResolutionService.GetReadyState();
        IsSuperResolutionAvailable = readyState is
            ImageSuperResolutionReadyState.Ready or
            ImageSuperResolutionReadyState.PreparationNeeded;
    }

    private void UpdateCanToggleSuperResolution()
    {
        CanToggleSuperResolution = IsLoaded &&
            IsSuperResolutionFeatureEnabled &&
            IsSuperResolutionAvailable &&
            !IsSuperResolutionGenerating &&
            (IsSuperResolutionActive || IsAiFeatureRequestAllowed(AiFeatureId.ImageSuperResolution));
    }

    private void UpdateTextExtractionAvailability()
    {
        IsTextExtractionFeatureEnabled = _textExtractionFeatureAvailability.IsTextExtractionEnabled;
        if (!IsTextExtractionFeatureEnabled)
        {
            IsTextExtractionAvailable = false;
            return;
        }

        TextExtractionReadyState readyState = _textExtractionService.GetReadyState();
        IsTextExtractionAvailable = readyState is
            TextExtractionReadyState.Ready or
            TextExtractionReadyState.PreparationNeeded;
    }

    private void UpdateCanToggleTextExtraction()
    {
        CanToggleTextExtraction = IsLoaded &&
            IsTextExtractionFeatureEnabled &&
            IsTextExtractionAvailable &&
            (IsTextExtractionModeActive || !IsTextExtractionRunning);
    }

    private void UpdateImageDescriptionAvailability()
    {
        IsImageDescriptionFeatureEnabled = _imageDescriptionFeatureAvailability.IsImageDescriptionEnabled;
        if (!IsImageDescriptionFeatureEnabled)
        {
            IsImageDescriptionAvailable = false;
            return;
        }

        ImageDescriptionReadyState readyState = _imageDescriptionService.GetReadyState();
        IsImageDescriptionAvailable = readyState is
            ImageDescriptionReadyState.Ready or
            ImageDescriptionReadyState.PreparationNeeded;
    }

    private void UpdateCanToggleImageDescription()
    {
        CanToggleImageDescription = IsLoaded &&
            IsImageDescriptionFeatureEnabled &&
            IsImageDescriptionAvailable &&
            (IsImageDescriptionModeActive || !IsImageDescriptionRunning);
    }

    private void UpdateCanGenerateImageDescription()
    {
        CanGenerateImageDescription = IsLoaded &&
            IsImageDescriptionModeActive &&
            IsImageDescriptionAvailable;
    }

    private void UpdateForegroundExtractionAvailability()
    {
        IsForegroundExtractionFeatureEnabled = _imageForegroundExtractionFeatureAvailability.IsImageForegroundExtractionEnabled;
        if (!IsForegroundExtractionFeatureEnabled)
        {
            IsForegroundExtractionAvailable = false;
            return;
        }

        ForegroundExtractionReadyState readyState = _imageForegroundExtractionService.GetReadyState();
        IsForegroundExtractionAvailable = readyState is
            ForegroundExtractionReadyState.Ready or
            ForegroundExtractionReadyState.PreparationNeeded;
    }

    private void UpdateCanToggleForegroundExtraction()
    {
        CanToggleForegroundExtraction = IsLoaded &&
            IsForegroundExtractionFeatureEnabled &&
            IsForegroundExtractionAvailable &&
            (IsForegroundExtractionModeActive || !IsForegroundExtractionRunning);
    }

    private void UpdateObjectEraseAvailability()
    {
        IsObjectEraseFeatureEnabled = _imageObjectEraseFeatureAvailability.IsImageObjectEraseEnabled;
        if (!IsObjectEraseFeatureEnabled)
        {
            IsObjectEraseAvailable = false;
            return;
        }

        ObjectEraseReadyState readyState = _imageObjectEraseService.GetReadyState();
        IsObjectEraseAvailable = readyState is
            ObjectEraseReadyState.Ready or
            ObjectEraseReadyState.PreparationNeeded;
    }

    private void UpdateCanToggleObjectErase()
    {
        CanToggleObjectErase = IsLoaded &&
            IsObjectEraseFeatureEnabled &&
            IsObjectEraseAvailable &&
            (IsObjectEraseModeActive || !IsObjectEraseRunning);
    }

    private void UpdateObjectExtractionAvailability()
    {
        IsObjectExtractionFeatureEnabled = _imageObjectExtractionFeatureAvailability.IsImageObjectExtractionEnabled;
        if (!IsObjectExtractionFeatureEnabled)
        {
            IsObjectExtractionAvailable = false;
            return;
        }

        ForegroundExtractionReadyState readyState = _imageForegroundExtractionService.GetReadyState();
        IsObjectExtractionAvailable = readyState is
            ForegroundExtractionReadyState.Ready or
            ForegroundExtractionReadyState.PreparationNeeded;
    }

    private void UpdateCanToggleObjectExtraction()
    {
        CanToggleObjectExtraction = IsLoaded &&
            IsObjectExtractionFeatureEnabled &&
            IsObjectExtractionAvailable &&
            (IsObjectExtractionModeActive || !IsObjectExtractionRunning);
    }

    private bool IsAiFeatureRequestAllowed(AiFeatureId featureId)
    {
        return _aiFeatureConsentService.GetConsentState(featureId) != AiFeatureConsentState.Denied;
    }

    private void IncrementEditRevision(bool preservePointSelectionAiMode = false)
    {
        _editRevision++;
        ClearImageDescriptionResults();

        if (IsTextExtractionModeActive)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.TextExtraction));
        }
        else if (IsImageDescriptionModeActive)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.ImageDescription));
        }
        else if (IsForegroundExtractionModeActive && !preservePointSelectionAiMode)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.ForegroundExtraction));
        }
        else if (IsObjectEraseModeActive && !preservePointSelectionAiMode)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.ObjectErase));
        }
        else if (IsObjectExtractionModeActive && !preservePointSelectionAiMode)
        {
            ApplyActiveMode(_modeStateMachine.Deactivate(ImageEditMode.ObjectExtraction));
        }
    }

    private void CancelSuperResolutionWork()
    {
        _superResolutionCancellationTokenSource?.Cancel();
        _superResolutionCancellationTokenSource?.Dispose();
        _superResolutionCancellationTokenSource = null;
    }

    private void CancelTextExtractionWork()
    {
        _textExtractionCancellationTokenSource?.Cancel();
        _textExtractionCancellationTokenSource?.Dispose();
        _textExtractionCancellationTokenSource = null;
    }

    private void CancelImageDescriptionWork()
    {
        _imageDescriptionCancellationTokenSource?.Cancel();
        _imageDescriptionCancellationTokenSource = null;
        _runningImageDescriptionMode = null;
        IsImageDescriptionRunning = false;
        NotifyImageDescriptionCommandsCanExecuteChanged();
        RaiseImageDescriptionSelectionProperties();
    }

    private void CancelForegroundExtractionWork()
    {
        _foregroundExtractionCancellationTokenSource?.Cancel();
        _foregroundExtractionCancellationTokenSource = null;
        IsForegroundExtractionRunning = false;
    }

    private void CancelObjectEraseWork()
    {
        _objectEraseCancellationTokenSource?.Cancel();
        _objectEraseCancellationTokenSource = null;
        IsObjectEraseRunning = false;
    }

    private void CancelObjectExtractionWork()
    {
        _objectExtractionCancellationTokenSource?.Cancel();
        _objectExtractionCancellationTokenSource = null;
        IsObjectExtractionRunning = false;
    }

    private void ClearDisplayedImageDescription()
    {
        ImageDescription = string.Empty;
        ImageDescriptionStatusMessage = string.Empty;
        SelectedImageDescriptionMode = null;
        RaiseImageDescriptionSelectionProperties();
    }

    private void ShowImageDescription(ImageDescriptionMode mode, string description)
    {
        ImageDescriptionStatusMessage = string.Empty;
        ImageDescription = description;
        SelectedImageDescriptionMode = mode;
        RaiseImageDescriptionSelectionProperties();
    }

    private void ClearImageDescriptionResults()
    {
        _imageDescriptionResults.Clear();
        ClearDisplayedImageDescription();
    }

    private void NotifyImageDescriptionCommandsCanExecuteChanged()
    {
        GenerateBriefImageDescriptionCommand.NotifyCanExecuteChanged();
        GenerateDetailedImageDescriptionCommand.NotifyCanExecuteChanged();
        GenerateDiagramImageDescriptionCommand.NotifyCanExecuteChanged();
        GenerateAccessibleImageDescriptionCommand.NotifyCanExecuteChanged();
    }

    private void RaiseImageDescriptionSelectionProperties()
    {
        RaisePropertyChanged(nameof(IsBriefImageDescriptionSelected));
        RaisePropertyChanged(nameof(IsDetailedImageDescriptionSelected));
        RaisePropertyChanged(nameof(IsDiagramImageDescriptionSelected));
        RaisePropertyChanged(nameof(IsAccessibleImageDescriptionSelected));
    }

    private string GetReadyStateFailureMessage(ImageSuperResolutionReadyState readyState)
    {
        return readyState switch
        {
            ImageSuperResolutionReadyState.NotSupported => GetLocalizedString("ImageSuperResolutionStatus_NotSupported"),
            ImageSuperResolutionReadyState.Disabled => GetLocalizedString("ImageSuperResolutionStatus_Disabled"),
            _ => GetLocalizedString("ImageSuperResolutionStatus_NotAvailable")
        };
    }

    private string GetPreparationFailureMessage(ImageSuperResolutionPreparationResult result)
    {
        return result.Status switch
        {
            ImageSuperResolutionPreparationStatus.Cancelled => string.Empty,
            ImageSuperResolutionPreparationStatus.NotSupported => GetLocalizedString("ImageSuperResolutionStatus_NotSupported"),
            ImageSuperResolutionPreparationStatus.Failed => string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? GetLocalizedString("ImageSuperResolutionStatus_PreparationFailed")
                : result.ErrorMessage,
            _ => GetLocalizedString("ImageSuperResolutionStatus_PreparationFailed")
        };
    }

    private string GetGenerationFailureMessage(ImageSuperResolutionResult result)
    {
        return result.Status switch
        {
            ImageSuperResolutionStatus.Cancelled => string.Empty,
            ImageSuperResolutionStatus.NotReady => GetLocalizedString("ImageSuperResolutionStatus_NotReady"),
            ImageSuperResolutionStatus.NotSupported => GetLocalizedString("ImageSuperResolutionStatus_NotSupported"),
            ImageSuperResolutionStatus.TooLarge => string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? GetLocalizedString("ImageSuperResolutionStatus_TooLarge")
                : result.ErrorMessage,
            ImageSuperResolutionStatus.Failed => string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? GetLocalizedString("ImageSuperResolutionStatus_Failed")
                : result.ErrorMessage,
            _ => GetLocalizedString("ImageSuperResolutionStatus_Failed")
        };
    }

    private void ShowSuperResolutionFailure(string message)
    {
        SuperResolutionStatusMessage = message;
        TrackEditTool(
            "super_resolution",
            string.IsNullOrWhiteSpace(message)
                ? TelemetryOutcomes.Canceled
                : TelemetryOutcomes.Failed);
        if (!string.IsNullOrWhiteSpace(message))
        {
            _notificationService.ShowError(message);
        }
    }

    private string GetReadyStateFailureMessage(TextExtractionReadyState readyState)
    {
        return readyState switch
        {
            TextExtractionReadyState.NotSupported => GetLocalizedString("TextExtractionStatus_NotSupported"),
            TextExtractionReadyState.Disabled => GetLocalizedString("TextExtractionStatus_Disabled"),
            _ => GetLocalizedString("TextExtractionStatus_NotAvailable")
        };
    }

    private string GetPreparationFailureMessage(TextExtractionPreparationResult result)
    {
        return result.Status switch
        {
            TextExtractionPreparationStatus.Cancelled => string.Empty,
            TextExtractionPreparationStatus.NotSupported => GetLocalizedString("TextExtractionStatus_NotSupported"),
            TextExtractionPreparationStatus.Failed => string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? GetLocalizedString("TextExtractionStatus_PreparationFailed")
                : result.ErrorMessage,
            _ => GetLocalizedString("TextExtractionStatus_PreparationFailed")
        };
    }

    private string GetExtractionFailureMessage(TextExtractionResult result)
    {
        return result.Status switch
        {
            TextExtractionStatus.Cancelled => string.Empty,
            TextExtractionStatus.NotReady => GetLocalizedString("TextExtractionStatus_NotReady"),
            TextExtractionStatus.NotSupported => GetLocalizedString("TextExtractionStatus_NotSupported"),
            TextExtractionStatus.TooLarge => string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? GetLocalizedString("TextExtractionStatus_TooLarge")
                : result.ErrorMessage,
            TextExtractionStatus.Failed => string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? GetLocalizedString("TextExtractionStatus_Failed")
                : result.ErrorMessage,
            _ => GetLocalizedString("TextExtractionStatus_Failed")
        };
    }

    private void ShowTextExtractionFailure(string message)
    {
        TextExtractionStatusMessage = message;
        TrackEditTool(
            "text_extraction",
            string.IsNullOrWhiteSpace(message)
                ? TelemetryOutcomes.Canceled
                : TelemetryOutcomes.Failed);
        if (!string.IsNullOrWhiteSpace(message))
        {
            _notificationService.ShowError(message);
        }
    }

    private string GetImageDescriptionReadyStateFailureMessage(ImageDescriptionReadyState readyState)
    {
        return readyState switch
        {
            ImageDescriptionReadyState.NotSupported => GetLocalizedString("ImageDescriptionStatus_NotSupported"),
            ImageDescriptionReadyState.Disabled => GetLocalizedString("ImageDescriptionStatus_Disabled"),
            _ => GetLocalizedString("ImageDescriptionStatus_NotAvailable")
        };
    }

    private string GetImageDescriptionPreparationFailureMessage(ImageDescriptionPreparationResult result)
    {
        return result.Status switch
        {
            ImageDescriptionPreparationStatus.Cancelled => string.Empty,
            ImageDescriptionPreparationStatus.NotSupported => GetLocalizedString("ImageDescriptionStatus_NotSupported"),
            _ => GetLocalizedString("ImageDescriptionStatus_PreparationFailed")
        };
    }

    private string GetImageDescriptionFailureMessage(ImageDescriptionResult result)
    {
        return result.Status switch
        {
            ImageDescriptionStatus.Cancelled => string.Empty,
            ImageDescriptionStatus.NotReady => GetLocalizedString("ImageDescriptionStatus_NotReady"),
            ImageDescriptionStatus.NotSupported => GetLocalizedString("ImageDescriptionStatus_NotSupported"),
            ImageDescriptionStatus.BlockedByPolicy => GetLocalizedString("ImageDescriptionStatus_BlockedByPolicy"),
            ImageDescriptionStatus.BlockedByContentSafety => GetLocalizedString("ImageDescriptionStatus_BlockedByContentSafety"),
            ImageDescriptionStatus.TooMuchText => GetLocalizedString("ImageDescriptionStatus_TooMuchText"),
            _ => GetLocalizedString("ImageDescriptionStatus_Failed")
        };
    }

    private void ShowImageDescriptionFailure(string message)
    {
        ImageDescriptionStatusMessage = message;
        TrackEditTool(
            _runningImageDescriptionMode is { } mode
                ? GetImageDescriptionTelemetryTool(mode)
                : "image_description",
            string.IsNullOrWhiteSpace(message)
                ? TelemetryOutcomes.Canceled
                : TelemetryOutcomes.Failed);
        if (!string.IsNullOrWhiteSpace(message))
        {
            _notificationService.ShowError(message);
        }
    }

    private string GetForegroundExtractionReadyStateFailureMessage(ForegroundExtractionReadyState readyState)
    {
        return readyState switch
        {
            ForegroundExtractionReadyState.NotSupported => GetLocalizedString("ForegroundExtractionStatus_NotSupported"),
            ForegroundExtractionReadyState.Disabled => GetLocalizedString("ForegroundExtractionStatus_Disabled"),
            _ => GetLocalizedString("ForegroundExtractionStatus_NotAvailable")
        };
    }

    private string GetForegroundExtractionPreparationFailureMessage(ForegroundExtractionPreparationResult result)
    {
        return result.Status switch
        {
            ForegroundExtractionPreparationStatus.Cancelled => string.Empty,
            ForegroundExtractionPreparationStatus.NotSupported => GetLocalizedString("ForegroundExtractionStatus_NotSupported"),
            ForegroundExtractionPreparationStatus.Failed when !string.IsNullOrWhiteSpace(result.ErrorMessage) => result.ErrorMessage,
            _ => GetLocalizedString("ForegroundExtractionStatus_PreparationFailed")
        };
    }

    private string GetForegroundExtractionFailureMessage(ForegroundExtractionResult result)
    {
        return result.Status switch
        {
            ForegroundExtractionStatus.Cancelled => string.Empty,
            ForegroundExtractionStatus.NotReady => GetLocalizedString("ForegroundExtractionStatus_NotReady"),
            ForegroundExtractionStatus.NotSupported => GetLocalizedString("ForegroundExtractionStatus_NotSupported"),
            ForegroundExtractionStatus.Failed when !string.IsNullOrWhiteSpace(result.ErrorMessage) => result.ErrorMessage,
            _ => GetLocalizedString("ForegroundExtractionStatus_Failed")
        };
    }

    private void ShowForegroundExtractionFailure(string message)
    {
        ForegroundExtractionStatusMessage = message;
        TrackEditTool(
            "foreground_extraction",
            string.IsNullOrWhiteSpace(message)
                ? TelemetryOutcomes.Canceled
                : TelemetryOutcomes.Failed);
        if (!string.IsNullOrWhiteSpace(message))
        {
            _notificationService.ShowError(message);
        }
    }

    private string GetObjectEraseReadyStateFailureMessage(ObjectEraseReadyState readyState)
    {
        return readyState switch
        {
            ObjectEraseReadyState.NotSupported => GetLocalizedString("ObjectEraseStatus_NotSupported"),
            ObjectEraseReadyState.Disabled => GetLocalizedString("ObjectEraseStatus_Disabled"),
            _ => GetLocalizedString("ObjectEraseStatus_NotAvailable")
        };
    }

    private string GetObjectErasePreparationFailureMessage(ObjectErasePreparationResult result)
    {
        return result.Status switch
        {
            ObjectErasePreparationStatus.Cancelled => string.Empty,
            ObjectErasePreparationStatus.NotSupported => GetLocalizedString("ObjectEraseStatus_NotSupported"),
            ObjectErasePreparationStatus.Failed when !string.IsNullOrWhiteSpace(result.ErrorMessage) => result.ErrorMessage,
            _ => GetLocalizedString("ObjectEraseStatus_PreparationFailed")
        };
    }

    private string GetObjectEraseFailureMessage(ObjectEraseResult result)
    {
        return result.Status switch
        {
            ObjectEraseStatus.Cancelled => string.Empty,
            ObjectEraseStatus.NotReady => GetLocalizedString("ObjectEraseStatus_NotReady"),
            ObjectEraseStatus.NotSupported => GetLocalizedString("ObjectEraseStatus_NotSupported"),
            ObjectEraseStatus.Failed when !string.IsNullOrWhiteSpace(result.ErrorMessage) => result.ErrorMessage,
            _ => GetLocalizedString("ObjectEraseStatus_Failed")
        };
    }

    private void ShowObjectEraseFailure(string message)
    {
        ObjectEraseStatusMessage = message;
        TrackEditTool(
            "object_erase",
            string.IsNullOrWhiteSpace(message)
                ? TelemetryOutcomes.Canceled
                : TelemetryOutcomes.Failed);
        if (!string.IsNullOrWhiteSpace(message))
        {
            _notificationService.ShowError(message);
        }
    }

    private string GetObjectExtractionReadyStateFailureMessage(ForegroundExtractionReadyState readyState)
    {
        return readyState switch
        {
            ForegroundExtractionReadyState.NotSupported => GetLocalizedString("ObjectExtractionStatus_NotSupported"),
            ForegroundExtractionReadyState.Disabled => GetLocalizedString("ObjectExtractionStatus_Disabled"),
            _ => GetLocalizedString("ObjectExtractionStatus_NotAvailable")
        };
    }

    private string GetObjectExtractionPreparationFailureMessage(ForegroundExtractionPreparationResult result)
    {
        return result.Status switch
        {
            ForegroundExtractionPreparationStatus.Cancelled => string.Empty,
            ForegroundExtractionPreparationStatus.NotSupported => GetLocalizedString("ObjectExtractionStatus_NotSupported"),
            ForegroundExtractionPreparationStatus.Failed when !string.IsNullOrWhiteSpace(result.ErrorMessage) => result.ErrorMessage,
            _ => GetLocalizedString("ObjectExtractionStatus_PreparationFailed")
        };
    }

    private string GetObjectExtractionFailureMessage(ForegroundExtractionResult result)
    {
        return result.Status switch
        {
            ForegroundExtractionStatus.Cancelled => string.Empty,
            ForegroundExtractionStatus.NotReady => GetLocalizedString("ObjectExtractionStatus_NotReady"),
            ForegroundExtractionStatus.NotSupported => GetLocalizedString("ObjectExtractionStatus_NotSupported"),
            ForegroundExtractionStatus.Failed when !string.IsNullOrWhiteSpace(result.ErrorMessage) => result.ErrorMessage,
            _ => GetLocalizedString("ObjectExtractionStatus_Failed")
        };
    }

    private void ShowObjectExtractionFailure(string message)
    {
        ObjectExtractionStatusMessage = message;
        TrackEditTool(
            "object_extraction",
            string.IsNullOrWhiteSpace(message)
                ? TelemetryOutcomes.Canceled
                : TelemetryOutcomes.Failed);
        if (!string.IsNullOrWhiteSpace(message))
        {
            _notificationService.ShowError(message);
        }
    }

    private string GetLocalizedString(string resourceKey)
    {
        string value = _localizationService.GetString(resourceKey);
        return string.IsNullOrWhiteSpace(value)
            ? resourceKey
            : value;
    }

    private sealed class PermissiveAiFeatureConsentService : IAiFeatureConsentService
    {
        public IReadOnlyList<AiFeatureConsent> GetFeatureConsents()
        {
            return [];
        }

        public AiFeatureConsentState GetConsentState(AiFeatureId featureId)
        {
            return AiFeatureConsentState.Granted;
        }

        public Task SetConsentAsync(AiFeatureId featureId, bool isGranted, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class PermissiveAiFeatureConsentDialogService : IAiFeatureConsentDialogService
    {
        public Task<bool> RequestConsentAsync(AiFeatureId featureId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class DisabledTextExtractionFeatureAvailability : ITextExtractionFeatureAvailability
    {
        public bool IsTextExtractionEnabled => false;
    }

    private sealed class DisabledImageDescriptionFeatureAvailability : IImageDescriptionFeatureAvailability
    {
        public bool IsImageDescriptionEnabled => false;
    }

    private sealed class DisabledImageForegroundExtractionFeatureAvailability : IImageForegroundExtractionFeatureAvailability
    {
        public bool IsImageForegroundExtractionEnabled => false;
    }

    private sealed class DisabledImageObjectEraseFeatureAvailability : IImageObjectEraseFeatureAvailability
    {
        public bool IsImageObjectEraseEnabled => false;
    }

    private sealed class DisabledImageObjectExtractionFeatureAvailability : IImageObjectExtractionFeatureAvailability
    {
        public bool IsImageObjectExtractionEnabled => false;
    }

    private sealed class NullTextExtractionService : ITextExtractionService
    {
        public TextExtractionReadyState GetReadyState()
        {
            return TextExtractionReadyState.NotSupported;
        }

        public Task<TextExtractionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TextExtractionPreparationResult.NotSupported);
        }

        public Task<TextExtractionResult> ExtractAsync(
            TextExtractionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TextExtractionResult.NotSupported);
        }
    }

    private sealed class NullImageDescriptionService : IImageDescriptionService
    {
        public ImageDescriptionReadyState GetReadyState()
        {
            return ImageDescriptionReadyState.NotSupported;
        }

        public Task<ImageDescriptionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ImageDescriptionPreparationResult.NotSupported);
        }

        public Task<ImageDescriptionResult> DescribeAsync(
            ImageDescriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ImageDescriptionResult.NotSupported);
        }
    }

    private sealed class NullImageForegroundExtractionService : IImageForegroundExtractionService
    {
        public ForegroundExtractionReadyState GetReadyState()
        {
            return ForegroundExtractionReadyState.NotSupported;
        }

        public Task<ForegroundExtractionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ForegroundExtractionPreparationResult.NotSupported);
        }

        public Task<ForegroundExtractionResult> ExtractAsync(
            ForegroundExtractionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ForegroundExtractionResult.NotSupported);
        }
    }

    private sealed class NullImageObjectEraseService : IImageObjectEraseService
    {
        public ObjectEraseReadyState GetReadyState()
        {
            return ObjectEraseReadyState.NotSupported;
        }

        public Task<ObjectErasePreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ObjectErasePreparationResult.NotSupported);
        }

        public Task<ObjectEraseResult> EraseAsync(
            ObjectEraseRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ObjectEraseResult.NotSupported);
        }
    }
}
