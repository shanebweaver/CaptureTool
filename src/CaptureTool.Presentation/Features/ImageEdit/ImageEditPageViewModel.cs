using CaptureTool.Application.Features.Store;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Domain.Edit.Abstractions;
using CaptureTool.Domain.Edit.Abstractions.ChromaKey;
using CaptureTool.Domain.Edit.Abstractions.Drawable;
using CaptureTool.Domain.Edit.Abstractions.Operations;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Cancellation;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Share;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Abstractions.Windowing;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed partial class ImageEditPageViewModel : AsyncLoadableViewModelBase<ImageFile>
{
    private static readonly Color[] ShapesColorPalette = [
        Color.Transparent,
        Color.FromArgb(31, 41, 55),
        Color.FromArgb(249, 250, 251),
        Color.FromArgb(239, 68, 68),
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
    private readonly IWindowHandleProvider _windowingService;
    private readonly ICancellationService _cancellationService;
    private readonly IImageCanvasPrinter _imageCanvasPrinter;
    private readonly IImageCanvasExporter _imageCanvasExporter;
    private readonly IChromaKeyService _chromaKeyService;
    private readonly IFilePickerService _filePickerService;
    private readonly IFeatureManager _featureManager;
    private readonly IShareService _shareService;

    private ImageDrawable? _imageDrawable;
    private readonly Stack<CanvasOperation> _operationsUndoStack;
    private readonly Stack<CanvasOperation> _operationsRedoStack;

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

    public bool IsShapesFeatureEnabled
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
        IWindowHandleProvider windowingService,
        ICancellationService cancellationService,
        ITelemetryService telemetryService,
        IImageCanvasPrinter imageCanvasPrinter,
        IImageCanvasExporter imageCanvasExporter,
        IFilePickerService filePickerService,
        IChromaKeyService chromaKeyService,
        IFeatureManager featureManager,
        IShareService shareService)
    {
        _localizationService = localizationService;
        _storeService = storeService;
        _windowingService = windowingService;
        _cancellationService = cancellationService;
        _imageCanvasPrinter = imageCanvasPrinter;
        _chromaKeyService = chromaKeyService;
        _filePickerService = filePickerService;
        _featureManager = featureManager;
        _shareService = shareService;
        _imageCanvasExporter = imageCanvasExporter;

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
        ShapeStrokeColor = ShapesColorPalette[3]; // Red
        ShapeFillColor = ShapesColorPalette[0]; // Transparent
        ShapeStrokeColorOptions = ShapesColorPalette;
        ShapeFillColorOptions = ShapesColorPalette;
        ShapeStrokeWidth = 3;
        ShapeStrokeOpacity = 100;
        ShapeFillOpacity = 100;
        ZoomPercentage = 100;
        IsAutoZoomLocked = false;
        _operationsUndoStack = [];
        _operationsRedoStack = [];

        CopyCommand = new AsyncRelayCommand(CopyAsync);
        ToggleCropModeCommand = new RelayCommand(ToggleCropMode);
        ToggleShapesModeCommand = new RelayCommand(ToggleShapesMode, () => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes));
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        UndoCommand = new RelayCommand(Undo);
        RedoCommand = new RelayCommand(Redo);
        RotateCommand = new RelayCommand(Rotate);
        FlipHorizontalCommand = new RelayCommand(() => Flip(FlipDirection.Horizontal));
        FlipVerticalCommand = new RelayCommand(() => Flip(FlipDirection.Vertical));
        PrintCommand = new AsyncRelayCommand(PrintAsync);
        ShareCommand = new AsyncRelayCommand(ShareAsync);
        UpdateChromaKeyColorCommand = new RelayCommand<Color>(UpdateChromaKeyColor, (c) => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_ChromaKey));
        UpdateOrientationCommand = new RelayCommand<ImageOrientation>(UpdateOrientation);
        UpdateCropRectCommand = new RelayCommand<Rectangle>(UpdateCropRect);
        UpdateShowChromaKeyOptionsCommand = new RelayCommand<bool>(UpdateShowChromaKeyOptions);
        UpdateDesaturationCommand = new RelayCommand<int>(UpdateDesaturation);
        UpdateToleranceCommand = new RelayCommand<int>(UpdateTolerance);
        UpdateSelectedColorOptionIndexCommand = new RelayCommand<int>(UpdateSelectedColorOptionIndex);
        UpdateSelectedShapeTypeCommand = new RelayCommand<ShapeType>(UpdateSelectedShapeType, (_) => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes));
        UpdateShapeStrokeColorCommand = new RelayCommand<Color>(UpdateShapeStrokeColor, (_) => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes));
        UpdateShapeFillColorCommand = new RelayCommand<Color>(UpdateShapeFillColor, (_) => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes));
        UpdateShapeStrokeWidthCommand = new RelayCommand<int>(UpdateShapeStrokeWidth, (_) => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes));
        UpdateShapeStrokeOpacityCommand = new RelayCommand<int>(UpdateShapeStrokeOpacity, (_) => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes));
        UpdateShapeFillOpacityCommand = new RelayCommand<int>(UpdateShapeFillOpacity, (_) => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes));
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

            _imageDrawable = new(topLeft, imageFile, ImageSize);
            _drawables.Add(_imageDrawable);

            if (_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_ChromaKey))
            {
                bool isChromaKeyAddOnOwned = true; //await _storeService.IsAddonPurchasedAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
                IsChromaKeyAddOnOwned = isChromaKeyAddOnOwned;
                if (isChromaKeyAddOnOwned)
                {
                    // Empty option disables the effect.
                    _chromaKeyColorOptions.Add(ChromaKeyColorOption.Empty);

                    // Add top detected colors
                    var topColors = await _chromaKeyService.GetTopColorsAsync(imageFile, 5, 4);
                    foreach (var topColor in topColors)
                    {
                        ChromaKeyColorOption colorOption = new(topColor);
                        _chromaKeyColorOptions.Add(colorOption);
                    }
                }
            }

            IsShapesFeatureEnabled = _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes);

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
        CropRect = Rectangle.Empty;
        ImageSize = Size.Empty;
        Orientation = ImageOrientation.RotateNoneFlipNone;
        MirroredDisplayName = string.Empty;
        _drawables.Clear();
        base.Dispose();
    }

    private void UpdateDesaturation(int value)
    {
        ChromaKeyDesaturation = value;
        UpdateChromaKeyEffectValues();
    }

    private void UpdateTolerance(int value)
    {
        ChromaKeyTolerance = value;
        UpdateChromaKeyEffectValues();
    }

    private void UpdateSelectedColorOptionIndex(int value)
    {
        SelectedChromaKeyColorOption = value;
        UpdateChromaKeyColor(value);
    }

    private void UpdateShowChromaKeyOptions(bool value)
    {
        ShowChromaKeyOptions = value;
        if (value)
        {
            if (IsInCropMode)
            {
                IsInCropMode = false;
            }
            if (IsInShapesMode)
            {
                IsInShapesMode = false;
            }
        }
    }

    private void UpdateIsInCropMode(bool value)
    {
        IsInCropMode = value;
        if (value)
        {
            if (ShowChromaKeyOptions)
            {
                ShowChromaKeyOptions = false;
            }
            if (IsInShapesMode)
            {
                IsInShapesMode = false;
            }
        }
    }

    private async Task CopyAsync()
    {
        ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
        await _imageCanvasExporter.CopyImageToClipboardAsync([.. Drawables], options);
    }

    private void ToggleCropMode()
    {
        UpdateIsInCropMode(!IsInCropMode);
    }

    private void ToggleShapesMode()
    {
        UpdateIsInShapesMode(!IsInShapesMode);
    }

    private void UpdateIsInShapesMode(bool value)
    {
        IsInShapesMode = value;
        if (value)
        {
            // Disable other modes when shapes mode is enabled
            if (IsInCropMode)
            {
                IsInCropMode = false;
            }
            if (ShowChromaKeyOptions)
            {
                ShowChromaKeyOptions = false;
            }
        }
    }

    private void UpdateSelectedShapeType(ShapeType value)
    {
        if (!_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes))
        {
            return;
        }
        SelectedShapeType = value;
    }

    private void UpdateShapeStrokeColor(Color value)
    {
        if (!_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes))
        {
            return;
        }
        ShapeStrokeColor = ApplyOpacity(value, ShapeStrokeOpacity);
    }

    private void UpdateShapeFillColor(Color value)
    {
        if (!_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes))
        {
            return;
        }
        ShapeFillColor = ApplyOpacity(value, ShapeFillOpacity);
    }

    private void UpdateShapeStrokeWidth(int value)
    {
        if (!_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes))
        {
            return;
        }
        ShapeStrokeWidth = value;
    }

    private void UpdateShapeStrokeOpacity(int value)
    {
        if (!_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes))
        {
            return;
        }

        ShapeStrokeOpacity = Math.Clamp(value, 0, 100);
        ShapeStrokeColor = ApplyOpacity(ShapeStrokeColor, ShapeStrokeOpacity);
    }

    private void UpdateShapeFillOpacity(int value)
    {
        if (!_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes))
        {
            return;
        }

        ShapeFillOpacity = Math.Clamp(value, 0, 100);
        ShapeFillColor = ApplyOpacity(ShapeFillColor, ShapeFillOpacity);
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
        if (!IsInShapesMode || !_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes))
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

    public void OnShapeDeleted(int shapeIndex)
    {
        if (!IsInShapesMode || !_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes))
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
        if (!IsInShapesMode || !_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_Shapes))
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
        if (!_featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_ChromaKey))
        {
            return;
        }

        ChromaKeyColor = color;
        UpdateChromaKeyEffectValues();
    }

    private async Task SaveAsync()
    {
        nint hwnd = _windowingService.GetMainWindowHandle();
        IFile? file = await _filePickerService.PickSaveFileAsync(hwnd, FilePickerType.Image, UserFolder.Pictures);

        if (file is null)
        {
            return;
        }

        ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
        await _imageCanvasExporter.SaveImageAsync(file.FilePath, [.. Drawables], options);
    }

    private ImageCanvasRenderOptions GetImageCanvasRenderOptions()
    {
        return new(Orientation, ImageSize, CropRect);
    }

    private void Undo()
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

    private void Redo()
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

    private void Rotate()
    {
        ImageOrientation oldOrientation = Orientation;
        ImageOrientation newOrientation = ImageOrientationHelper.GetRotatedOrientation(oldOrientation, RotationDirection.Clockwise);
        Rectangle newCropRect = ImageOrientationHelper.GetOrientedCropRect(CropRect, ImageSize, oldOrientation, newOrientation);

        CropRect = newCropRect;
        Orientation = newOrientation;
        MirroredDisplayName = GetMirroredDisplayName(Orientation);
        RotationDisplayName = GetRotationDisplayName(Orientation);

        _operationsRedoStack.Clear();
        _operationsUndoStack.Push(new OrientationOperation(UpdateOrientation, oldOrientation, newOrientation));
        UpdateUndoRedoStackProperties();
    }

    private void Flip(FlipDirection flipDirection)
    {
        ImageOrientation oldOrientation = Orientation;
        ImageOrientation newOrientation = ImageOrientationHelper.GetFlippedOrientation(Orientation, flipDirection);
        Size imageSize = ImageOrientationHelper.GetOrientedImageSize(ImageSize, Orientation);
        Rectangle newCropRect = ImageOrientationHelper.GetFlippedCropRect(CropRect, imageSize, flipDirection);

        CropRect = newCropRect;
        Orientation = newOrientation;
        MirroredDisplayName = GetMirroredDisplayName(newOrientation);
        RotationDisplayName = GetRotationDisplayName(Orientation);

        _operationsRedoStack.Clear();
        _operationsUndoStack.Push(new OrientationOperation(UpdateOrientation, oldOrientation, newOrientation));
        UpdateUndoRedoStackProperties();
    }

    private void UpdateUndoRedoStackProperties()
    {
        HasUndoStack = _operationsUndoStack.Count > 0;
        HasRedoStack = _operationsRedoStack.Count > 0;
    }

    private async Task PrintAsync()
    {
        nint hwnd = _windowingService.GetMainWindowHandle();
        await _imageCanvasPrinter.ShowPrintUIAsync([.. Drawables], GetImageCanvasRenderOptions(), hwnd);
    }

    private async Task ShareAsync()
    {
        if (ImageFile == null)
        {
            throw new InvalidOperationException("No image to share");
        }

        nint hwnd = _windowingService.GetMainWindowHandle();
        ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
        using MemoryStream renderedStream = await _imageCanvasExporter.RenderToStreamAsync([.. Drawables], options);
        await _shareService.ShareStreamAsync(renderedStream, hwnd);
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
        Rectangle newCropRect = ImageOrientationHelper.GetOrientedCropRect(CropRect, ImageSize, oldOrientation, newOrientation);
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
}
