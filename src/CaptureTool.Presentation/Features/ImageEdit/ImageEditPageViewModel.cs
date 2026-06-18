using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Features.ImageEdit.Rendering;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed partial class ImageEditPageViewModel : AsyncLoadableViewModelBase<ImageFile>
{
    private readonly ILocalizationService _localizationService;
    private readonly ICancellationService _cancellationService;
    private readonly IImageCanvasPrinter _imageCanvasPrinter;
    private readonly IImageCanvasExporter _imageCanvasExporter;
    private readonly IFilePickerService _filePickerService;
    private readonly IShareService _shareService;

    private readonly ImageEditHistory _editHistory;
    private readonly ImageEditModeStateMachine _modeStateMachine;
    private ImageDrawable? _imageDrawable;
    private ImageEditSession _editSession;

    public event EventHandler? InvalidateCanvasRequested;
    public event EventHandler? ForceZoomAndCenterRequested;

    public IAsyncRelayCommand CopyCommand { get; }
    public IRelayCommand ToggleCropModeCommand { get; }
    public IRelayCommand ToggleShapesModeCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand RotateCommand { get; }
    public IRelayCommand FlipHorizontalCommand { get; }
    public IRelayCommand FlipVerticalCommand { get; }
    public IAsyncRelayCommand PrintCommand { get; }
    public IAsyncRelayCommand ShareCommand { get; }
    public IRelayCommand<ImageOrientation> UpdateOrientationCommand { get; }
    public IRelayCommand<Rectangle> UpdateCropRectCommand { get; }
    public IRelayCommand<bool> SetChromaKeyModeActiveCommand { get; }
    public IRelayCommand ToggleTextModeCommand { get; }
    public IRelayCommand<int> UpdateZoomPercentageCommand { get; }
    public IRelayCommand<bool> UpdateAutoZoomLockCommand { get; }
    public IRelayCommand ZoomAndCenterCommand { get; }

    public ChromaKeyToolViewModel ChromaKeyTool { get; }

    public ShapeToolViewModel ShapeTool { get; }

    public TextToolViewModel TextTool { get; }

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

    public ImageEditPageViewModel(
        ILocalizationService localizationService,
        ICancellationService cancellationService,
        IImageCanvasPrinter imageCanvasPrinter,
        IImageCanvasExporter imageCanvasExporter,
        IFilePickerService filePickerService,
        IShareService shareService,
        ChromaKeyToolViewModel chromaKeyTool,
        ShapeToolViewModel shapeTool,
        TextToolViewModel textTool)
    {
        _localizationService = localizationService;
        _cancellationService = cancellationService;
        _imageCanvasPrinter = imageCanvasPrinter;
        _filePickerService = filePickerService;
        _shareService = shareService;
        _imageCanvasExporter = imageCanvasExporter;

        ChromaKeyTool = chromaKeyTool;
        ShapeTool = shapeTool;
        TextTool = textTool;
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

        CopyCommand = new AsyncRelayCommand(CopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleCropModeCommand = new RelayCommand(ToggleCropMode);
        ToggleShapesModeCommand = new RelayCommand(ToggleShapesMode);
        SaveCommand = new AsyncRelayCommand(SaveAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UndoCommand = new RelayCommand(Undo);
        RedoCommand = new RelayCommand(Redo);
        RotateCommand = new RelayCommand(Rotate);
        FlipHorizontalCommand = new RelayCommand(() => Flip(FlipDirection.Horizontal));
        FlipVerticalCommand = new RelayCommand(() => Flip(FlipDirection.Vertical));
        PrintCommand = new AsyncRelayCommand(PrintAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ShareCommand = new AsyncRelayCommand(ShareAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateOrientationCommand = new RelayCommand<ImageOrientation>(UpdateOrientation);
        UpdateCropRectCommand = new RelayCommand<Rectangle>(UpdateCropRect);
        SetChromaKeyModeActiveCommand = new RelayCommand<bool>(SetChromaKeyModeActive);
        ToggleTextModeCommand = new RelayCommand(ToggleTextMode);
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

    public override async Task LoadAsync(ImageFile imageFile, CancellationToken cancellationToken)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            Vector2 topLeft = Vector2.Zero;
            ImageFile = imageFile;
            _editSession = new ImageEditSession(_filePickerService.GetImageFileSize(imageFile));
            SyncImageGeometryFromSession();
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
    }

    public override void Dispose()
    {
        ChromaKeyTool.SettingsChanged -= ChromaKeyTool_SettingsChanged;
        ChromaKeyTool.InteractionCommitted -= ChromaKeyTool_InteractionCommitted;
        _imageDrawable = null;
        _editHistory.Clear();
        ApplyActiveMode(_modeStateMachine.Reset());
        _editSession = new ImageEditSession(Size.Empty, ImageOrientation.RotateNoneFlipNone, Rectangle.Empty);
        SyncImageGeometryFromSession();
        SyncDrawablesFromSession();
        ChromaKeyTool.Reset();
        UpdateUndoRedoStackProperties();
        base.Dispose();
    }

    private async Task CopyAsync()
    {
        ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
        await _imageCanvasExporter.CopyImageToClipboardAsync([.. Drawables], options);
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

    private void ApplyActiveMode(ImageEditMode mode)
    {
        IsCropModeActive = mode == ImageEditMode.Crop;
        IsShapesModeActive = mode == ImageEditMode.Shapes;
        IsTextModeActive = mode == ImageEditMode.Text;
        IsChromaKeyModeActive = mode == ImageEditMode.ChromaKey;
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
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateChromaKeyEffectValues()
    {
        _editSession.SetChromaKeySettings(ChromaKeyTool.CaptureSettings());
        SyncDrawablesFromSession();

        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveAsync()
    {
        IFile? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Image, UserFolder.Pictures);

        if (file is null)
        {
            return;
        }

        ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
        await _imageCanvasExporter.SaveImageAsync(file.FilePath, [.. Drawables], options);
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
}
