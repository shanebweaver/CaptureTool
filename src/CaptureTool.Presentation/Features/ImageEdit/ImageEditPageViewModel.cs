using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.Application.Abstractions.Features.ImageEdit.Rendering;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed partial class ImageEditPageViewModel : AsyncLoadableViewModelBase<ImageFile>
{
    private const int MinimumShapeStrokeWidth = 1;
    private const int MaximumShapeStrokeWidth = 100;
    private const int MinimumTextFontSize = 1;
    private const int MaximumTextFontSize = 400;

    private enum ImageEditMode
    {
        None,
        Crop,
        Shapes,
        Text,
        ChromaKey
    }

    private static readonly Color[] DrawablesColorPalette = [
        Color.Transparent,
        Color.FromArgb(31, 41, 55), // White
        Color.FromArgb(249, 250, 251), // Black
        Color.FromArgb(239, 68, 68), // Red
        Color.FromArgb(249, 115, 22),
        Color.FromArgb(245, 158, 11),
        Color.FromArgb(234, 179, 8),
        Color.FromArgb(132, 204, 22),
        Color.FromArgb(34, 197, 94),
        Color.FromArgb(20, 184, 166),
        Color.FromArgb(6, 182, 212),
        Color.FromArgb(59, 130, 246),
        Color.FromArgb(99, 102, 241),
        Color.FromArgb(139, 92, 246),
        Color.FromArgb(236, 72, 153),
        Color.FromArgb(244, 63, 94),
    ];

    private readonly ILocalizationService _localizationService;
    private readonly IStoreService _storeService;
    private readonly ICancellationService _cancellationService;
    private readonly IImageCanvasPrinter _imageCanvasPrinter;
    private readonly IImageCanvasExporter _imageCanvasExporter;
    private readonly IChromaKeyService _chromaKeyService;
    private readonly IFilePickerService _filePickerService;
    private readonly IChromaKeyFeatureAvailability _chromaKeyFeatureAvailability;
    private readonly IShareService _shareService;
    private readonly ITelemetryService _telemetryService;

    private ImageDrawable? _imageDrawable;
    private readonly Stack<CanvasOperation> _operationsUndoStack;
    private readonly Stack<CanvasOperation> _operationsRedoStack;
    private ImageEditMode _activeEditMode;
    private ChromaKeyOperation.ChromaKeyState? _pendingChromaKeyInteractionState;

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
    public IRelayCommand<Color> UpdateChromaKeyColorCommand { get; }
    public IRelayCommand<ImageOrientation> UpdateOrientationCommand { get; }
    public IRelayCommand<Rectangle> UpdateCropRectCommand { get; }
    public IRelayCommand<bool> UpdateShowChromaKeyOptionsCommand { get; }
    public IRelayCommand<int> UpdateDesaturationCommand { get; }
    public IRelayCommand<int> UpdateToleranceCommand { get; }
    public IRelayCommand<int> UpdateSelectedColorOptionIndexCommand { get; }
    public IRelayCommand<ShapeType> UpdateSelectedShapeTypeCommand { get; }
    public IRelayCommand<Color> UpdateShapeStrokeColorCommand { get; }
    public IRelayCommand<Color> UpdateShapeFillColorCommand { get; }
    public IRelayCommand<int> UpdateShapeStrokeWidthCommand { get; }
    public IRelayCommand<int> UpdateShapeStrokeOpacityCommand { get; }
    public IRelayCommand<int> UpdateShapeFillOpacityCommand { get; }
    public IRelayCommand ToggleTextModeCommand { get; }
    public IRelayCommand<Color> UpdateTextFontColorCommand { get; }
    public IRelayCommand<Color> UpdateTextBackgroundColorCommand { get; }
    public IRelayCommand<int> UpdateTextFontColorOpacityCommand { get; }
    public IRelayCommand<int> UpdateTextBackgroundColorOpacityCommand { get; }
    public IRelayCommand<string?> UpdateTextFontFamilyCommand { get; }
    public IRelayCommand<int> UpdateTextFontSizeCommand { get; }
    public IRelayCommand<int> UpdateZoomPercentageCommand { get; }
    public IRelayCommand<bool> UpdateAutoZoomLockCommand { get; }
    public IRelayCommand ZoomAndCenterCommand { get; }

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

    private ObservableCollection<IDrawable> _drawables = [];

    public IReadOnlyList<IDrawable> Drawables
    {
        get => _drawables;
        private set
        {
            _drawables = value as ObservableCollection<IDrawable> ?? new ObservableCollection<IDrawable>(value);
            RaisePropertyChanged(nameof(Drawables));
        }
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

    public bool IsInCropMode
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsInShapesMode
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsInTextMode
    {
        get;
        private set => Set(ref field, value);
    }

    public ShapeType SelectedShapeType
    {
        get;
        private set => Set(ref field, value);
    }

    public Color ShapeStrokeColor
    {
        get;
        private set => Set(ref field, value);
    }

    public Color ShapeFillColor
    {
        get;
        private set => Set(ref field, value);
    }

    public IReadOnlyList<Color> ShapeStrokeColorOptions { get; }

    public IReadOnlyList<Color> ShapeFillColorOptions { get; }

    public int ShapeStrokeWidth
    {
        get;
        private set => Set(ref field, value);
    }

    public int ShapeStrokeOpacity
    {
        get;
        private set => Set(ref field, value);
    }

    public int ShapeFillOpacity
    {
        get;
        private set => Set(ref field, value);
    }

    public Color TextFontColor
    {
        get;
        private set => Set(ref field, value);
    }

    public Color TextBackgroundColor
    {
        get;
        private set => Set(ref field, value);
    }

    public IReadOnlyList<Color> TextFontColorOptions { get; }

    public IReadOnlyList<Color> TextBackgroundColorOptions { get; }

    public int TextFontColorOpacity
    {
        get;
        private set => Set(ref field, value);
    }

    public int TextBackgroundColorOpacity
    {
        get;
        private set => Set(ref field, value);
    }

    public string TextFontFamily
    {
        get;
        private set => Set(ref field, value);
    }

    public int TextFontSize
    {
        get;
        private set => Set(ref field, value);
    }

    public Rectangle CropRect
    {
        get;
        private set => Set(ref field, value);
    }

    public bool ShowChromaKeyOptions
    {
        get;
        private set => Set(ref field, value);
    }

    public int ChromaKeyTolerance
    {
        get;
        private set => Set(ref field, value);
    }

    public int ChromaKeyDesaturation
    {
        get;
        private set => Set(ref field, value);
    }

    public Color ChromaKeyColor
    {
        get;
        private set => Set(ref field, value);
    }

    private ObservableCollection<ChromaKeyColorOption> _chromaKeyColorOptions = [];

    public IReadOnlyList<ChromaKeyColorOption> ChromaKeyColorOptions
    {
        get => _chromaKeyColorOptions;
        private set
        {
            _chromaKeyColorOptions = value as ObservableCollection<ChromaKeyColorOption> ?? new ObservableCollection<ChromaKeyColorOption>(value);
            RaisePropertyChanged(nameof(ChromaKeyColorOptions));
        }
    }

    public int SelectedChromaKeyColorOption
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsChromaKeyAddOnOwned
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
        IStoreService storeService,
        ICancellationService cancellationService,
        ITelemetryService telemetryService,
        IImageCanvasPrinter imageCanvasPrinter,
        IImageCanvasExporter imageCanvasExporter,
        IFilePickerService filePickerService,
        IChromaKeyService chromaKeyService,
        IChromaKeyFeatureAvailability chromaKeyFeatureAvailability,
        IShareService shareService)
    {
        _localizationService = localizationService;
        _storeService = storeService;
        _cancellationService = cancellationService;
        _imageCanvasPrinter = imageCanvasPrinter;
        _chromaKeyService = chromaKeyService;
        _filePickerService = filePickerService;
        _chromaKeyFeatureAvailability = chromaKeyFeatureAvailability;
        _shareService = shareService;
        _imageCanvasExporter = imageCanvasExporter;
        _telemetryService = telemetryService;

        Drawables = [];
        ImageSize = new();
        Orientation = ImageOrientation.RotateNoneFlipNone;
        MirroredDisplayName = GetMirroredDisplayName(Orientation);
        RotationDisplayName = GetRotationDisplayName(Orientation);
        CropRect = Rectangle.Empty;
        SelectedChromaKeyColorOption = 0;
        ChromaKeyTolerance = 30;
        ChromaKeyColor = Color.Empty;
        ChromaKeyColorOptions = [];
        SelectedShapeType = ShapeType.Rectangle;
        ShapeStrokeColor = DrawablesColorPalette[3]; // Red
        ShapeFillColor = DrawablesColorPalette[0]; // Transparent
        ShapeStrokeColorOptions = DrawablesColorPalette;
        ShapeFillColorOptions = DrawablesColorPalette;
        TextFontColorOptions = DrawablesColorPalette;
        TextBackgroundColorOptions = DrawablesColorPalette;
        ShapeStrokeWidth = 3;
        ShapeStrokeOpacity = 100;
        ShapeFillOpacity = 100;
        TextFontColor = DrawablesColorPalette[2]; // Black
        TextBackgroundColor = DrawablesColorPalette[1]; // White
        TextFontColorOpacity = 100;
        TextBackgroundColorOpacity = 100;
        TextFontFamily = TextDrawable.DefaultFontFamily;
        TextFontSize = (int)TextDrawable.DefaultFontSize;
        ZoomPercentage = 100;
        IsAutoZoomLocked = false;
        _operationsUndoStack = [];
        _operationsRedoStack = [];

        CopyCommand = new AsyncRelayCommand(CopyAsync);
        ToggleCropModeCommand = new RelayCommand(ToggleCropMode);
        ToggleShapesModeCommand = new RelayCommand(ToggleShapesMode);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        UndoCommand = new RelayCommand(Undo);
        RedoCommand = new RelayCommand(Redo);
        RotateCommand = new RelayCommand(Rotate);
        FlipHorizontalCommand = new RelayCommand(() => Flip(FlipDirection.Horizontal));
        FlipVerticalCommand = new RelayCommand(() => Flip(FlipDirection.Vertical));
        PrintCommand = new AsyncRelayCommand(PrintAsync);
        ShareCommand = new AsyncRelayCommand(ShareAsync);
        UpdateChromaKeyColorCommand = new RelayCommand<Color>(UpdateChromaKeyColor, (c) => _chromaKeyFeatureAvailability.IsChromaKeyEnabled);
        UpdateOrientationCommand = new RelayCommand<ImageOrientation>(UpdateOrientation);
        UpdateCropRectCommand = new RelayCommand<Rectangle>(UpdateCropRect);
        UpdateShowChromaKeyOptionsCommand = new RelayCommand<bool>(UpdateShowChromaKeyOptions);
        UpdateDesaturationCommand = new RelayCommand<int>(UpdateDesaturation);
        UpdateToleranceCommand = new RelayCommand<int>(UpdateTolerance);
        UpdateSelectedColorOptionIndexCommand = new RelayCommand<int>(UpdateSelectedColorOptionIndex);
        UpdateSelectedShapeTypeCommand = new RelayCommand<ShapeType>(UpdateSelectedShapeType);
        UpdateShapeStrokeColorCommand = new RelayCommand<Color>(UpdateShapeStrokeColor);
        UpdateShapeFillColorCommand = new RelayCommand<Color>(UpdateShapeFillColor);
        UpdateShapeStrokeWidthCommand = new RelayCommand<int>(UpdateShapeStrokeWidth);
        UpdateShapeStrokeOpacityCommand = new RelayCommand<int>(UpdateShapeStrokeOpacity);
        UpdateShapeFillOpacityCommand = new RelayCommand<int>(UpdateShapeFillOpacity);
        ToggleTextModeCommand = new RelayCommand(ToggleTextMode);
        UpdateTextFontColorCommand = new RelayCommand<Color>(UpdateTextFontColor);
        UpdateTextBackgroundColorCommand = new RelayCommand<Color>(UpdateTextBackgroundColor);
        UpdateTextFontColorOpacityCommand = new RelayCommand<int>(UpdateTextFontColorOpacity);
        UpdateTextBackgroundColorOpacityCommand = new RelayCommand<int>(UpdateTextBackgroundColorOpacity);
        UpdateTextFontFamilyCommand = new RelayCommand<string?>(UpdateTextFontFamily);
        UpdateTextFontSizeCommand = new RelayCommand<int>(UpdateTextFontSize);
        UpdateZoomPercentageCommand = new RelayCommand<int>(UpdateZoomPercentage);
        UpdateAutoZoomLockCommand = new RelayCommand<bool>(UpdateAutoZoomLock);
        ZoomAndCenterCommand = new RelayCommand(RequestZoomAndCenter);
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
            ImageSize = _filePickerService.GetImageFileSize(imageFile);
            CropRect = new(Point.Empty, ImageSize);
            ApplyImageSizeBasedDefaults(ImageSize);

            _imageDrawable = new(topLeft, imageFile, ImageSize);
            _drawables.Add(_imageDrawable);

            if (_chromaKeyFeatureAvailability.IsChromaKeyEnabled)
            {
                bool isChromaKeyAddOnOwned = true;// await _storeService.IsAddonPurchasedAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
                IsChromaKeyAddOnOwned = isChromaKeyAddOnOwned;
                if (isChromaKeyAddOnOwned)
                {
                    // Empty option disables the effect.
                    _chromaKeyColorOptions.Add(ChromaKeyColorOption.Empty);

                    // Add top detected colors
                    var topColors = await _chromaKeyService.GetTopColorsAsync(imageFile, 15, 16);
                    foreach (var topColor in topColors)
                    {
                        ChromaKeyColorOption colorOption = new(topColor);
                        _chromaKeyColorOptions.Add(colorOption);
                    }
                }
            }

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
        _imageDrawable = null;
        _pendingChromaKeyInteractionState = null;
        SetActiveEditMode(ImageEditMode.None);
        CropRect = Rectangle.Empty;
        ImageSize = Size.Empty;
        Orientation = ImageOrientation.RotateNoneFlipNone;
        MirroredDisplayName = string.Empty;
        _drawables.Clear();
        base.Dispose();
    }

    private void UpdateDesaturation(int value)
    {
        ChromaKeyDesaturation = Math.Clamp(value, 0, 100);
        UpdateChromaKeyEffectValues();
    }

    private void UpdateTolerance(int value)
    {
        ChromaKeyTolerance = Math.Clamp(value, 0, 100);
        UpdateChromaKeyEffectValues();
    }

    private void UpdateSelectedColorOptionIndex(int value)
    {
        SelectedChromaKeyColorOption = value;
        UpdateChromaKeyColor(value);
    }

    public void BeginChromaKeyInteraction()
    {
        _pendingChromaKeyInteractionState ??= CaptureChromaKeyState();
    }

    public void CompleteChromaKeyInteraction()
    {
        if (_pendingChromaKeyInteractionState is not { } oldState)
        {
            return;
        }

        _pendingChromaKeyInteractionState = null;
        ChromaKeyOperation.ChromaKeyState newState = CaptureChromaKeyState();

        if (oldState.Equals(newState))
        {
            return;
        }

        _operationsUndoStack.Push(new ChromaKeyOperation(
            ApplyChromaKeyState,
            oldState,
            newState));
        _operationsRedoStack.Clear();
        UpdateUndoRedoStackProperties();
    }

    private void UpdateShowChromaKeyOptions(bool value)
    {
        if (value)
        {
            SetActiveEditMode(ImageEditMode.ChromaKey);
        }
        else if (_activeEditMode == ImageEditMode.ChromaKey)
        {
            SetActiveEditMode(ImageEditMode.None);
        }
    }

    private void UpdateIsInCropMode(bool value)
    {
        SetActiveEditMode(value ? ImageEditMode.Crop : ImageEditMode.None);
    }

    private async Task CopyAsync()
    {
        try
        {
            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            await _imageCanvasExporter.CopyImageToClipboardAsync([.. Drawables], options);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(CopyAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(CopyAsync), exception);
        }
    }

    private void ToggleCropMode()
    {
        UpdateIsInCropMode(!IsInCropMode);
    }

    private void ToggleShapesMode()
    {
        UpdateIsInShapesMode(!IsInShapesMode);
    }

    private void ToggleTextMode()
    {
        UpdateIsInTextMode(!IsInTextMode);
    }

    private void UpdateIsInShapesMode(bool value)
    {
        SetActiveEditMode(value ? ImageEditMode.Shapes : ImageEditMode.None);
    }

    private void UpdateIsInTextMode(bool value)
    {
        SetActiveEditMode(value ? ImageEditMode.Text : ImageEditMode.None);
    }

    private void SetActiveEditMode(ImageEditMode mode)
    {
        if (_activeEditMode == mode)
        {
            return;
        }

        _activeEditMode = mode;
        IsInCropMode = mode == ImageEditMode.Crop;
        IsInShapesMode = mode == ImageEditMode.Shapes;
        IsInTextMode = mode == ImageEditMode.Text;
        ShowChromaKeyOptions = mode == ImageEditMode.ChromaKey;
    }

    private void UpdateSelectedShapeType(ShapeType value)
    {
        SelectedShapeType = value;
    }

    private void UpdateShapeStrokeColor(Color value)
    {
        ShapeStrokeColor = ApplyOpacity(value, ShapeStrokeOpacity);
    }

    private void UpdateShapeFillColor(Color value)
    {
        ShapeFillColor = ApplyOpacity(value, ShapeFillOpacity);
    }

    private void UpdateShapeStrokeWidth(int value)
    {
        ShapeStrokeWidth = Math.Clamp(value, MinimumShapeStrokeWidth, MaximumShapeStrokeWidth);
    }

    private void UpdateShapeStrokeOpacity(int value)
    {
        ShapeStrokeOpacity = Math.Clamp(value, 0, 100);
        ShapeStrokeColor = ApplyOpacity(ShapeStrokeColor, ShapeStrokeOpacity);
    }

    private void UpdateShapeFillOpacity(int value)
    {
        ShapeFillOpacity = Math.Clamp(value, 0, 100);
        ShapeFillColor = ApplyOpacity(ShapeFillColor, ShapeFillOpacity);
    }

    private void UpdateTextFontColor(Color value)
    {
        TextFontColor = ApplyOpacity(value, TextFontColorOpacity);
    }

    private void UpdateTextBackgroundColor(Color value)
    {
        TextBackgroundColor = ApplyOpacity(value, TextBackgroundColorOpacity);
    }

    private void UpdateTextFontColorOpacity(int value)
    {
        TextFontColorOpacity = Math.Clamp(value, 0, 100);
        TextFontColor = ApplyOpacity(TextFontColor, TextFontColorOpacity);
    }

    private void UpdateTextBackgroundColorOpacity(int value)
    {
        TextBackgroundColorOpacity = Math.Clamp(value, 0, 100);
        TextBackgroundColor = ApplyOpacity(TextBackgroundColor, TextBackgroundColorOpacity);
    }

    private void UpdateTextFontFamily(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        TextFontFamily = value;
    }

    private void UpdateTextFontSize(int value)
    {
        TextFontSize = Math.Clamp(value, MinimumTextFontSize, MaximumTextFontSize);
    }

    private void ApplyImageSizeBasedDefaults(Size imageSize)
    {
        int largestEdge = Math.Max(imageSize.Width, imageSize.Height);
        int smallestEdge = Math.Min(imageSize.Width, imageSize.Height);

        if (largestEdge > 0)
        {
            ShapeStrokeWidth = Math.Clamp(
                (int)Math.Round(largestEdge / 900d),
                3,
                MaximumShapeStrokeWidth);
        }

        if (smallestEdge > 0)
        {
            TextFontSize = Math.Clamp(
                (int)Math.Round(smallestEdge / 40d),
                (int)TextDrawable.DefaultFontSize,
                MaximumTextFontSize);
        }
    }

    private static Color ApplyOpacity(Color color, int opacityPercentage)
    {
        if (color.Equals(Color.Transparent))
        {
            return Color.Transparent;
        }

        int alpha = (int)Math.Round(Math.Clamp(opacityPercentage, 0, 100) / 100d * byte.MaxValue);
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    public void OnShapeDrawn(Vector2 startPoint, Vector2 endPoint)
    {
        if (!IsInShapesMode)
        {
            return;
        }

        IDrawable? newShape = null;

        switch (SelectedShapeType)
        {
            case ShapeType.Rectangle:
                {
                    float x = Math.Min(startPoint.X, endPoint.X);
                    float y = Math.Min(startPoint.Y, endPoint.Y);
                    float width = Math.Abs(endPoint.X - startPoint.X);
                    float height = Math.Abs(endPoint.Y - startPoint.Y);

                    // Only create shape if it has a minimum size (at least 2 pixels)
                    if (width >= 2 && height >= 2)
                    {
                        newShape = new RectangleDrawable(
                            new Vector2(x, y),
                            new Size((int)Math.Ceiling(width), (int)Math.Ceiling(height)),
                            ShapeStrokeColor,
                            ShapeFillColor,
                            ShapeStrokeWidth);
                    }
                    break;
                }
            case ShapeType.Ellipse:
                {
                    float x = Math.Min(startPoint.X, endPoint.X);
                    float y = Math.Min(startPoint.Y, endPoint.Y);
                    float width = Math.Abs(endPoint.X - startPoint.X);
                    float height = Math.Abs(endPoint.Y - startPoint.Y);

                    // Only create shape if it has a minimum size (at least 2 pixels)
                    if (width >= 2 && height >= 2)
                    {
                        newShape = new EllipseDrawable(
                            new Vector2(x, y),
                            new Size((int)Math.Ceiling(width), (int)Math.Ceiling(height)),
                            ShapeStrokeColor,
                            ShapeFillColor,
                            ShapeStrokeWidth);
                    }
                    break;
                }
            case ShapeType.Line:
                {
                    // Only create line if it has a minimum length (at least 2 pixels)
                    float distance = Vector2.Distance(startPoint, endPoint);
                    if (distance >= 2)
                    {
                        newShape = new LineDrawable(
                            startPoint,
                            endPoint,
                            ShapeStrokeColor,
                            ShapeStrokeWidth);
                    }
                    break;
                }
            case ShapeType.Arrow:
                {
                    // Only create arrow if it has a minimum length (at least 2 pixels)
                    float distance = Vector2.Distance(startPoint, endPoint);
                    if (distance >= 2)
                    {
                        newShape = new ArrowDrawable(
                            startPoint,
                            endPoint,
                            ShapeStrokeColor,
                            ShapeStrokeWidth);
                    }
                    break;
                }
        }

        if (newShape != null)
        {
            // Add to undo stack
            var operation = new AddShapeOperation(
                _drawables,
                newShape,
                () => InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty));

            _operationsUndoStack.Push(operation);
            _operationsRedoStack.Clear(); // Clear redo stack on new action
            UpdateUndoRedoStackProperties();

            // Execute the operation
            operation.Redo();
        }
    }

    public void OnTextBoxDrawn(Vector2 startPoint, Vector2 endPoint)
    {
        if (!IsInTextMode)
        {
            return;
        }

        float x = Math.Min(startPoint.X, endPoint.X);
        float y = Math.Min(startPoint.Y, endPoint.Y);
        float width = Math.Abs(endPoint.X - startPoint.X);
        float height = Math.Abs(endPoint.Y - startPoint.Y);

        if (width < 2 || height < 2)
        {
            return;
        }

        var newText = new TextDrawable(
            new Vector2(x, y),
            new Size((int)Math.Ceiling(width), (int)Math.Ceiling(height)),
            string.Empty,
            TextFontColor,
            TextBackgroundColor,
            TextFontFamily,
            TextFontSize);

        var operation = new AddShapeOperation(
            _drawables,
            newText,
            () => InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty));

        _operationsUndoStack.Push(operation);
        _operationsRedoStack.Clear();
        UpdateUndoRedoStackProperties();
        operation.Redo();
    }

    public void OnTextDrawableSelected(TextDrawable text)
    {
        TextFontColor = text.Color;
        TextBackgroundColor = text.BackgroundColor;
        TextFontColorOpacity = AlphaToOpacityPercentage(text.Color);
        TextBackgroundColorOpacity = AlphaToOpacityPercentage(text.BackgroundColor);
        TextFontFamily = text.FontFamily;
        TextFontSize = Math.Clamp((int)Math.Round(text.FontSize), MinimumTextFontSize, MaximumTextFontSize);
    }

    public void OnShapeDeleted(int shapeIndex)
    {
        if (!IsInShapesMode && !IsInTextMode)
        {
            return;
        }

        if (shapeIndex >= 0 && shapeIndex < _drawables.Count)
        {
            var shape = _drawables[shapeIndex];

            // Add to undo stack
            var operation = new DeleteShapeOperation(
                _drawables,
                shape,
                shapeIndex,
                () => InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty));

            _operationsUndoStack.Push(operation);
            _operationsRedoStack.Clear(); // Clear redo stack on new action
            UpdateUndoRedoStackProperties();

            // Execute the operation
            operation.Redo();
        }
    }

    public void OnShapeModified(int shapeIndex, ModifyShapeOperation.ShapeState oldState, ModifyShapeOperation.ShapeState newState)
    {
        if (!IsInShapesMode && !IsInTextMode)
        {
            return;
        }

        if (shapeIndex >= 0 && shapeIndex < _drawables.Count)
        {
            var currentShape = _drawables[shapeIndex];

            // Add to undo stack
            var operation = new ModifyShapeOperation(
                currentShape,
                oldState,
                newState,
                () => InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty));

            _operationsUndoStack.Push(operation);
            _operationsRedoStack.Clear(); // Clear redo stack on new action
            UpdateUndoRedoStackProperties();
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

        _drawables.Add(drawable);
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateChromaKeyEffectValues()
    {
        if (_imageDrawable != null && _imageDrawable.ImageEffect == null)
        {
            _imageDrawable.ImageEffect = new ImageChromaKeyEffect(ChromaKeyColor, ChromaKeyTolerance / 100f, ChromaKeyDesaturation / 100f)
            {
                IsEnabled = !ChromaKeyColor.IsEmpty
            };
        }
        else if (_imageDrawable?.ImageEffect is ImageChromaKeyEffect chromaKeyEffect)
        {
            chromaKeyEffect.Tolerance = ChromaKeyTolerance / 100f;
            chromaKeyEffect.Desaturation = ChromaKeyDesaturation / 100f;
            chromaKeyEffect.Color = ChromaKeyColor;
            chromaKeyEffect.IsEnabled = !ChromaKeyColor.IsEmpty;
        }

        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateChromaKeyColor(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= ChromaKeyColorOptions.Count)
        {
            return;
        }

        UpdateChromaKeyColor(ChromaKeyColorOptions[colorIndex].Color);
    }

    private void UpdateChromaKeyColor(Color color)
    {
        if (!_chromaKeyFeatureAvailability.IsChromaKeyEnabled)
        {
            return;
        }

        ChromaKeyColor = color;
        UpdateChromaKeyEffectValues();
    }

    private ChromaKeyOperation.ChromaKeyState CaptureChromaKeyState()
    {
        return new(
            SelectedChromaKeyColorOption,
            ChromaKeyColor,
            ChromaKeyTolerance,
            ChromaKeyDesaturation);
    }

    private void ApplyChromaKeyState(ChromaKeyOperation.ChromaKeyState state)
    {
        SelectedChromaKeyColorOption = state.SelectedColorOptionIndex;
        ChromaKeyColor = state.Color;
        ChromaKeyTolerance = state.Tolerance;
        ChromaKeyDesaturation = state.Desaturation;
        UpdateChromaKeyEffectValues();
    }

    private async Task SaveAsync()
    {
        try
        {
            IFile? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Image, UserFolder.Pictures);

            if (file is null)
            {
                return;
            }

            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            await _imageCanvasExporter.SaveImageAsync(file.FilePath, [.. Drawables], options);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(SaveAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(SaveAsync), exception);
        }
    }

    private ImageCanvasRenderOptions GetImageCanvasRenderOptions()
    {
        return new(Orientation, ImageSize, CropRect);
    }

    private void Undo()
    {
        try
        {
            if (_operationsUndoStack.Count == 0)
            {
                throw new InvalidOperationException("Cannot undo, the stack is empty.");
            }

            var operation = _operationsUndoStack.Pop();
            _operationsRedoStack.Push(operation);
            UpdateUndoRedoStackProperties();

            operation.Undo();
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(Undo), exception);
        }
    }

    private void Redo()
    {
        try
        {
            if (_operationsRedoStack.Count == 0)
            {
                throw new InvalidOperationException("Cannot undo, the stack is empty.");
            }

            var operation = _operationsRedoStack.Pop();
            _operationsUndoStack.Push(operation);
            UpdateUndoRedoStackProperties();

            operation.Redo();
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(Redo), exception);
        }
    }

    private void Rotate()
    {
        try
        {
            ImageOrientation oldOrientation = Orientation;
            ImageOrientation newOrientation = ImageOrientationGeometry.GetRotatedOrientation(oldOrientation, RotationDirection.Clockwise);
            Rectangle newCropRect = ImageOrientationGeometry.GetOrientedCropRect(CropRect, ImageSize, oldOrientation, newOrientation);

            CropRect = newCropRect;
            Orientation = newOrientation;
            MirroredDisplayName = GetMirroredDisplayName(Orientation);
            RotationDisplayName = GetRotationDisplayName(Orientation);

            _operationsRedoStack.Clear();
            _operationsUndoStack.Push(new OrientationOperation(UpdateOrientation, oldOrientation, newOrientation));
            UpdateUndoRedoStackProperties();
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(Rotate), exception);
        }
    }

    private void Flip(FlipDirection flipDirection)
    {
        try
        {
            ImageOrientation oldOrientation = Orientation;
            ImageOrientation newOrientation = ImageOrientationGeometry.GetFlippedOrientation(Orientation, flipDirection);
            Size imageSize = ImageOrientationGeometry.GetOrientedImageSize(ImageSize, Orientation);
            Rectangle newCropRect = ImageOrientationGeometry.GetFlippedCropRect(CropRect, imageSize, flipDirection);

            CropRect = newCropRect;
            Orientation = newOrientation;
            MirroredDisplayName = GetMirroredDisplayName(newOrientation);
            RotationDisplayName = GetRotationDisplayName(Orientation);

            _operationsRedoStack.Clear();
            _operationsUndoStack.Push(new OrientationOperation(UpdateOrientation, oldOrientation, newOrientation));
            UpdateUndoRedoStackProperties();
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(Flip), exception);
        }
    }

    private void UpdateUndoRedoStackProperties()
    {
        HasUndoStack = _operationsUndoStack.Count > 0;
        HasRedoStack = _operationsRedoStack.Count > 0;
    }

    private async Task PrintAsync()
    {
        try
        {
            await _imageCanvasPrinter.ShowPrintUIAsync([.. Drawables], GetImageCanvasRenderOptions());
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(PrintAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(PrintAsync), exception);
        }
    }

    private async Task ShareAsync()
    {
        try
        {
            if (ImageFile == null)
            {
                throw new InvalidOperationException("No image to share");
            }

            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            using MemoryStream renderedStream = await _imageCanvasExporter.RenderToStreamAsync([.. Drawables], options);
            await _shareService.ShareStreamAsync(renderedStream);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(ShareAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(ShareAsync), exception);
        }
    }

    public void OnCropInteractionComplete(Rectangle oldCropRect)
    {
        Rectangle newCropRect = CropRect;
        if (newCropRect != oldCropRect)
        {
            _operationsRedoStack.Clear();
            _operationsUndoStack.Push(new CropOperation(UpdateCropRect, oldCropRect, newCropRect));
            UpdateUndoRedoStackProperties();
        }
    }

    private void UpdateOrientation(ImageOrientation newOrientation)
    {
        ImageOrientation oldOrientation = Orientation;
        Rectangle newCropRect = ImageOrientationGeometry.GetOrientedCropRect(CropRect, ImageSize, oldOrientation, newOrientation);
        CropRect = newCropRect;
        Orientation = newOrientation;
        MirroredDisplayName = GetMirroredDisplayName(newOrientation);
        RotationDisplayName = GetRotationDisplayName(Orientation);
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCropRect(Rectangle newCropRect)
    {
        CropRect = newCropRect;
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
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

    private static int AlphaToOpacityPercentage(Color color)
    {
        return color.Equals(Color.Transparent)
            ? 100
            : (int)Math.Round(color.A / (double)byte.MaxValue * 100);
    }
}
