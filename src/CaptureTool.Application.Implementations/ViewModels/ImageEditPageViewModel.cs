using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.Store;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Domain.Edit.Interfaces;
using CaptureTool.Domain.Edit.Interfaces.ChromaKey;
using CaptureTool.Domain.Edit.Interfaces.Drawable;
using CaptureTool.Domain.Edit.Interfaces.Operations;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Cancellation;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Share;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Store;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Infrastructure.Interfaces.Windowing;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class ImageEditPageViewModel : AsyncLoadableViewModelBase<ImageFile>, IImageEditPageViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadImageEditPage";
        public static readonly string Copy = "Copy";
        public static readonly string ToggleCropMode = "ToggleCropMode";
        public static readonly string Save = "Save";
        public static readonly string Undo = "Undo";
        public static readonly string Redo = "Redo";
        public static readonly string Rotate = "Rotate";
        public static readonly string FlipHorizontal = "FlipHorizontal";
        public static readonly string FlipVertical = "FlipVertical";
        public static readonly string Print = "Print";
        public static readonly string Share = "Share";
        public static readonly string UpdateChromaKeyColor = "UpdateChromaKeyColor";
        public static readonly string UpdateOrientation = "UpdateOrientation";
        public static readonly string UpdateCropRect = "UpdateCropRect";
        public static readonly string UpdateShowChromaKeyOptions = "UpdateShowChromaKeyOptions";
        public static readonly string UpdateDesaturation = "UpdateDesaturation";
        public static readonly string UpdateTolerance = "UpdateTolerance";
        public static readonly string UpdateSelectedColorOptionIndex = "UpdateSelectedColorOptionIndex";
    }

    private const string TelemetryContext = "ImageEditPage";

    private readonly ILocalizationService _localizationService;
    private readonly IStoreService _storeService;
    private readonly IWindowHandleProvider _windowingService;
    private readonly ICancellationService _cancellationService;
    private readonly ITelemetryService _telemetryService;
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

    public IAsyncAppCommand CopyCommand { get; }
    public IAppCommand ToggleCropModeCommand { get; }
    public IAsyncAppCommand SaveCommand { get; }
    public IAppCommand UndoCommand { get; }
    public IAppCommand RedoCommand { get; }
    public IAppCommand RotateCommand { get; }
    public IAppCommand FlipHorizontalCommand { get; }
    public IAppCommand FlipVerticalCommand { get; }
    public IAsyncAppCommand PrintCommand { get; }
    public IAsyncAppCommand ShareCommand { get; }
    public IAppCommand<Color> UpdateChromaKeyColorCommand { get; }
    public IAppCommand<ImageOrientation> UpdateOrientationCommand { get; }
    public IAppCommand<Rectangle> UpdateCropRectCommand { get; }
    public IAppCommand<bool> UpdateShowChromaKeyOptionsCommand { get; }
    public IAppCommand<int> UpdateDesaturationCommand { get; }
    public IAppCommand<int> UpdateToleranceCommand { get; }
    public IAppCommand<int> UpdateSelectedColorOptionIndexCommand { get; }

    public bool HasUndoStack
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool HasRedoStack
    {
        get => field;
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
        get => field;
        private set => Set(ref field, value);
    }

    public Size ImageSize
    {
        get => field;
        private set => Set(ref field, value);
    }

    public ImageOrientation Orientation
    {
        get => field;
        private set => Set(ref field, value);
    }

    public string MirroredDisplayName
    {
        get => field;
        private set => Set(ref field, value);
    }

    public string RotationDisplayName
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsInCropMode
    {
        get => field;
        private set => Set(ref field, value);
    }

    public Rectangle CropRect
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool ShowChromaKeyOptions
    {
        get => field;
        private set => Set(ref field, value);
    }

    public int ChromaKeyTolerance
    {
        get => field;
        private set => Set(ref field, value);
    }

    public int ChromaKeyDesaturation
    {
        get => field;
        private set => Set(ref field, value);
    }

    public Color ChromaKeyColor
    {
        get => field;
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
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsChromaKeyAddOnOwned
    {
        get => field;
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
        _telemetryService = telemetryService;
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
        _operationsUndoStack = [];
        _operationsRedoStack = [];

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        CopyCommand = commandFactory.CreateAsync(ActivityIds.Copy, CopyAsync);
        ToggleCropModeCommand = commandFactory.Create(ActivityIds.ToggleCropMode, ToggleCropMode);
        SaveCommand = commandFactory.CreateAsync(ActivityIds.Save, SaveAsync);
        UndoCommand = commandFactory.Create(ActivityIds.Undo, Undo);
        RedoCommand = commandFactory.Create(ActivityIds.Redo, Redo);
        RotateCommand = commandFactory.Create(ActivityIds.Rotate, Rotate);
        FlipHorizontalCommand = commandFactory.Create(ActivityIds.FlipHorizontal, () => Flip(FlipDirection.Horizontal));
        FlipVerticalCommand = commandFactory.Create(ActivityIds.FlipVertical, () => Flip(FlipDirection.Vertical));
        PrintCommand = commandFactory.CreateAsync(ActivityIds.Print, PrintAsync);
        ShareCommand = commandFactory.CreateAsync(ActivityIds.Share, ShareAsync);
        UpdateChromaKeyColorCommand = commandFactory.Create<Color>(ActivityIds.UpdateChromaKeyColor, UpdateChromaKeyColor, (c) => _featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey));
        UpdateOrientationCommand = commandFactory.Create<ImageOrientation>(ActivityIds.UpdateOrientation, UpdateOrientation);
        UpdateCropRectCommand = commandFactory.Create<Rectangle>(ActivityIds.UpdateCropRect, UpdateCropRect);
        UpdateShowChromaKeyOptionsCommand = commandFactory.Create<bool>(ActivityIds.UpdateShowChromaKeyOptions, UpdateShowChromaKeyOptions);
        UpdateDesaturationCommand = commandFactory.Create<int>(ActivityIds.UpdateDesaturation, UpdateDesaturation);
        UpdateToleranceCommand = commandFactory.Create<int>(ActivityIds.UpdateTolerance, UpdateTolerance);
        UpdateSelectedColorOptionIndexCommand = commandFactory.Create<int>(ActivityIds.UpdateSelectedColorOptionIndex, UpdateSelectedColorOptionIndex);
    }

    public override Task LoadAsync(ImageFile imageFile, CancellationToken cancellationToken)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, TelemetryContext, ActivityIds.Load, async () =>
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

                if (_featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey))
                {
                    bool isChromaKeyAddOnOwned = await _storeService.IsAddonPurchasedAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval);
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

                InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                cts.Dispose();
            }

            await base.LoadAsync(imageFile, cancellationToken);
        });
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
        if (value && IsInCropMode)
        {
            IsInCropMode = false;
        }
    }

    private void UpdateIsInCropMode(bool value)
    {
        IsInCropMode = value;
        if (value && ShowChromaKeyOptions)
        {
            ShowChromaKeyOptions = false;
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
        if (!_featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey))
        {
            return;
        }

        ChromaKeyColor = color;
        UpdateChromaKeyEffectValues();
    }

    private async Task SaveAsync()
    {
        nint hwnd = _windowingService.GetMainWindowHandle();
        IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FilePickerType.Image, UserFolder.Pictures)
            ?? throw new OperationCanceledException("User cancelled the save file picker.");
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
        _operationsUndoStack.Push(new OrientationOperation(UpdateOrientationCommand, oldOrientation, newOrientation));
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
        _operationsUndoStack.Push(new OrientationOperation(UpdateOrientationCommand, oldOrientation, newOrientation));
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
        await _shareService.ShareAsync(ImageFile.FilePath, hwnd);
    }

    public void OnCropInteractionComplete(Rectangle oldCropRect)
    {
        Rectangle newCropRect = CropRect;
        if (newCropRect != oldCropRect)
        {
            _operationsRedoStack.Clear();
            _operationsUndoStack.Push(new CropOperation(UpdateCropRectCommand, oldCropRect, newCropRect));
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
}
