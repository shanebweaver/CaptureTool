using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Domain.Ai;
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed partial class ImageEditPageViewModel : AsyncLoadableViewModelBase<ImageFile>, IEditableSession
{
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
    private ImageDescriptionMode? _runningImageDescriptionMode;
    private readonly Dictionary<ImageDescriptionMode, string> _imageDescriptionResults = [];
    private bool _hasUnsavedChangesBeforeSuperResolution;
    private bool _hasUserEditsSinceSuperResolutionActivated;
    private int _editRevision;
    private int? _textExtractionProcessedRevision;

    public event EventHandler? InvalidateCanvasRequested;
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
        IImageDescriptionFeatureAvailability? imageDescriptionFeatureAvailability = null)
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
        ExecuteEditCommand(new SetChromaKeyCommand(settings.OldSettings, settings.NewSettings));
    }

    private void SettingsService_SettingsChanged(ISettingDefinition[] settings)
    {
        if (settings.Any(setting =>
            setting.Key == CaptureToolSettings.Settings_AiConsent_TextExtraction.Key ||
            setting.Key == CaptureToolSettings.Settings_AiConsent_ImageSuperResolution.Key ||
            setting.Key == CaptureToolSettings.Settings_AiConsent_ImageDescription.Key))
        {
            UpdateCanToggleSuperResolution();
            UpdateCanToggleTextExtraction();
            UpdateCanToggleImageDescription();
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
    }

    public override void Dispose()
    {
        CancelSuperResolutionWork();
        CancelTextExtractionWork();
        CancelImageDescriptionWork();
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
        ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
        await _imageCanvasExporter.CopyImageToClipboardAsync([.. Drawables], options);
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
        }
        catch (Exception)
        {
            _notificationService.ShowError(GetLocalizedString(ImageDescriptionCopyFailedMessageResourceKey));
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
            RefreshImageDescriptionToggleState();
            UpdateCanToggleImageDescription();
            return;
        }

        ApplyActiveMode(_modeStateMachine.Activate(ImageEditMode.ImageDescription));
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
        IsCropModeActive = mode == ImageEditMode.Crop;
        IsShapesModeActive = mode == ImageEditMode.Shapes;
        IsTextModeActive = mode == ImageEditMode.Text;
        IsChromaKeyModeActive = mode == ImageEditMode.ChromaKey;
        IsColorPickerModeActive = mode == ImageEditMode.ColorPicker;
        IsTextExtractionModeActive = mode == ImageEditMode.TextExtraction;
        IsImageDescriptionModeActive = mode == ImageEditMode.ImageDescription;

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

        UpdateCanToggleImageDescription();
        UpdateCanGenerateImageDescription();
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
            ExecuteEditCommand(new AddDrawableCommand(newShape));
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
            ExecuteEditCommand(new AddDrawableCommand(newText));
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
    }

    public void OnShapeDeleted(int shapeIndex)
    {
        if (!IsShapesModeActive && !IsTextModeActive)
        {
            return;
        }

        if (shapeIndex >= 0 && shapeIndex < _editSession.Drawables.Count)
        {
            ExecuteEditCommand(new DeleteDrawableCommand(shapeIndex));
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
            ExecuteEditCommand(new ModifyDrawableCommand(shapeIndex, oldState, newState));
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
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateChromaKeyEffectValues()
    {
        _editSession.SetChromaKeySettings(ChromaKeyTool.CaptureSettings());
        SyncDrawablesFromSession();

        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            FileReference? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Image, UserFolder.Pictures);

            if (file is null)
            {
                return false;
            }

            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            await _imageCanvasExporter.SaveImageAsync(file.FilePath, [.. Drawables], options);
            HasUnsavedChanges = false;
            _hasUnsavedChangesBeforeSuperResolution = false;
            _hasUserEditsSinceSuperResolutionActivated = false;
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to save image edits.");
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
        if (!_editHistory.Undo(_editSession))
        {
            return;
        }

        SyncImageGeometryFromSession();
        SyncDrawablesFromSession();
        SyncChromaKeySettingsFromSession();
        UpdateUndoRedoStackProperties();
        IncrementEditRevision();
        HasUnsavedChanges = true;
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Redo()
    {
        if (!_editHistory.Redo(_editSession))
        {
            return;
        }

        SyncImageGeometryFromSession();
        SyncDrawablesFromSession();
        SyncChromaKeySettingsFromSession();
        UpdateUndoRedoStackProperties();
        IncrementEditRevision();
        HasUnsavedChanges = true;
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
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
        await _imageCanvasPrinter.ShowPrintUIAsync([.. Drawables], GetImageCanvasRenderOptions());
    }

    private async Task ShareAsync()
    {
        if (ImageFile == null)
        {
            return;
        }

        ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
        using MemoryStream renderedStream = await _imageCanvasExporter.RenderToStreamAsync([.. Drawables], options);
        await _shareService.ShareStreamAsync(renderedStream);
    }

    private async Task EditInPaintAsync()
    {
        if (!IsLoaded)
        {
            return;
        }

        string imagePath = GetTemporaryPaintImagePath();
        await _imageCanvasExporter.SaveImageAsync(imagePath, [.. Drawables], GetImageCanvasRenderOptions());
        await _openExternalEditorAction.ExecuteAsync(
            new OpenExternalEditorRequest(imagePath, ExternalMediaEditor.Paint),
            CancellationToken.None);
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

    private void ExecuteEditCommand(IImageEditCommand command)
    {
        _editHistory.Execute(_editSession, command);
        SyncImageGeometryFromSession();
        SyncDrawablesFromSession();
        SyncChromaKeySettingsFromSession();
        UpdateUndoRedoStackProperties();
        IncrementEditRevision();
        MarkUnsavedChanges();
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
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
        }
        catch (OperationCanceledException)
        {
            TextExtractionStatusMessage = string.Empty;
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
        }
        catch (OperationCanceledException)
        {
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

    private bool IsAiFeatureRequestAllowed(AiFeatureId featureId)
    {
        return _aiFeatureConsentService.GetConsentState(featureId) != AiFeatureConsentState.Denied;
    }

    private void IncrementEditRevision()
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
}
