using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Store;
using CaptureTool.Core.Telemetry;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Edit.Interfaces;
using CaptureTool.Domains.Edit.Interfaces.ChromaKey;
using CaptureTool.Domains.Edit.Interfaces.Drawable;
using CaptureTool.Domains.Edit.Interfaces.Operations;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Share;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Store;
using CaptureTool.Services.Interfaces.Telemetry;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.ViewModels;

public sealed partial class ImageEditPageViewModel : AsyncLoadableViewModelBase<ImageFile>
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
    }

    private readonly IStoreService _storeService;
    private readonly IAppController _appController;
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

    public AsyncRelayCommand CopyCommand { get; }
    public RelayCommand ToggleCropModeCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand RotateCommand { get; }
    public RelayCommand FlipHorizontalCommand { get; }
    public RelayCommand FlipVerticalCommand { get; }
    public AsyncRelayCommand PrintCommand { get; }
    public AsyncRelayCommand ShareCommand { get; }
    public RelayCommand<Color> UpdateChromaKeyColorCommand { get; }

    public RelayCommand<ImageOrientation> UpdateOrientationCommand { get; }
    public RelayCommand<Rectangle> UpdateCropRectCommand { get; }
    public RelayCommand<bool> UpdateShowChromaKeyOptionsCommand { get; }
    public RelayCommand<int> UpdateDesaturationCommand { get; }
    public RelayCommand<int> UpdateToleranceCommand { get; }
    public RelayCommand<int> UpdateSelectedColorOptionIndexCommand { get; }

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

    public ObservableCollection<IDrawable> Drawables
    {
        get => field;
        private set => Set(ref field, value);
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

    public string OrientationDisplayName
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

    public ObservableCollection<ChromaKeyColorOption> ChromaKeyColorOptions
    {
        get => field;
        private set => Set(ref field, value);
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
        IStoreService storeService,
        IAppController appController,
        ICancellationService cancellationService,
        ITelemetryService telemetryService,
        IImageCanvasPrinter imageCanvasPrinter,
        IImageCanvasExporter imageCanvasExporter,
        IFilePickerService filePickerService,
        IChromaKeyService chromaKeyService,
        IFeatureManager featureManager,
        IShareService shareService)
    {
        _storeService = storeService;
        _appController = appController;
        _cancellationService = cancellationService;
        _telemetryService = telemetryService;
        _imageCanvasPrinter = imageCanvasPrinter;
        _chromaKeyService = chromaKeyService;
        _filePickerService = filePickerService;
        _featureManager = featureManager;
        _shareService = shareService;

        Drawables = [];
        ImageSize = new();
        Orientation = ImageOrientation.RotateNoneFlipNone;
        OrientationDisplayName = GetOrientationDisplayName(Orientation);
        CropRect = Rectangle.Empty;
        _imageCanvasExporter = imageCanvasExporter;
        SelectedChromaKeyColorOption = 0;
        ChromaKeyTolerance = 30;
        ChromaKeyColor = Color.Empty;
        ChromaKeyColorOptions = [];
        _operationsUndoStack = [];
        _operationsRedoStack = [];

        CopyCommand = new(CopyAsync);
        ToggleCropModeCommand = new(ToggleCropMode);
        SaveCommand = new(SaveAsync);
        UndoCommand = new(Undo);
        RedoCommand = new(Redo);
        RotateCommand = new(Rotate);
        FlipHorizontalCommand = new(() => Flip(FlipDirection.Horizontal));
        FlipVerticalCommand = new(() => Flip(FlipDirection.Vertical));
        PrintCommand = new(PrintAsync);
        ShareCommand = new(ShareAsync);
        UpdateChromaKeyColorCommand = new(UpdateChromaKeyColor, (c) => _featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey));
        UpdateOrientationCommand = new(UpdateOrientation);
        UpdateCropRectCommand = new(UpdateCropRect);
        UpdateShowChromaKeyOptionsCommand = new(UpdateShowChromaKeyOptions);
        UpdateDesaturationCommand = new(UpdateDesaturation);
        UpdateToleranceCommand = new(UpdateTolerance);
        UpdateSelectedColorOptionIndexCommand = new(UpdateSelectedColorOptionIndex);
    }

    public override Task LoadAsync(ImageFile imageFile, CancellationToken cancellationToken)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Load, async () =>
        {
            var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
            try
            {
                Vector2 topLeft = Vector2.Zero;
                ImageFile = imageFile;
                ImageSize = _filePickerService.GetImageFileSize(imageFile);
                CropRect = new(Point.Empty, ImageSize);

                _imageDrawable = new(topLeft, imageFile, ImageSize);
                Drawables.Add(_imageDrawable);

                if (_featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey))
                {
                    bool isChromaKeyAddOnOwned = await _storeService.IsAddonPurchasedAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval);
                    IsChromaKeyAddOnOwned = isChromaKeyAddOnOwned;
                    if (isChromaKeyAddOnOwned)
                    {
                        // Empty option disables the effect.
                        ChromaKeyColorOptions.Add(ChromaKeyColorOption.Empty);

                        // Add top detected colors
                        var topColors = await _chromaKeyService.GetTopColorsAsync(imageFile, 5, 4);
                        foreach (var topColor in topColors)
                        {
                            ChromaKeyColorOption colorOption = new(topColor);
                            ChromaKeyColorOptions.Add(colorOption);
                        }
                    }
                }

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
        OrientationDisplayName = string.Empty;
        Drawables.Clear();
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

    private Task CopyAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Copy, async () =>
        {
            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            await _imageCanvasExporter.CopyImageToClipboardAsync([.. Drawables], options);
        });
    }

    private void ToggleCropMode()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ToggleCropMode, () =>
        {
            UpdateIsInCropMode(!IsInCropMode);
        });
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

    private Task SaveAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Save, async () =>
        {
            nint hwnd = _appController.GetMainWindowHandle();
            IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FileType.Image, UserFolder.Pictures) 
                ?? throw new OperationCanceledException("User cancelled the save file picker.");
            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            await _imageCanvasExporter.SaveImageAsync(file.FilePath, [.. Drawables], options);
        });
    }

    public ImageCanvasRenderOptions GetImageCanvasRenderOptions()
    {
        return new(Orientation, ImageSize, CropRect);
    }

    private void Undo()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Undo, () =>
        {
            if (_operationsUndoStack.Count == 0)
            {
                throw new InvalidOperationException("Cannot undo, the stack is empty.");
            }

            var operation = _operationsUndoStack.Pop();
            _operationsRedoStack.Push(operation);
            UpdateUndoRedoStackProperties();

            operation.Undo();
        });
    }

    private void Redo()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Redo, () =>
        {
            if (_operationsRedoStack.Count == 0)
            {
                throw new InvalidOperationException("Cannot undo, the stack is empty.");
            }

            var operation = _operationsRedoStack.Pop();
            _operationsUndoStack.Push(operation);
            UpdateUndoRedoStackProperties();

            operation.Redo();
        });
    }

    private void Rotate()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Rotate, () =>
        {
            ImageOrientation oldOrientation = Orientation;
            ImageOrientation newOrientation = ImageOrientationHelper.GetRotatedOrientation(oldOrientation, RotationDirection.Clockwise);
            Rectangle newCropRect = ImageOrientationHelper.GetOrientedCropRect(CropRect, ImageSize, oldOrientation, newOrientation);

            CropRect = newCropRect;
            Orientation = newOrientation;
            OrientationDisplayName = GetOrientationDisplayName(newOrientation);

            _operationsRedoStack.Clear();
            _operationsUndoStack.Push(new OrientationOperation(UpdateOrientationCommand, oldOrientation, newOrientation));
            UpdateUndoRedoStackProperties();
        });
    }

    private void UpdateUndoRedoStackProperties()
    {
        HasUndoStack = _operationsUndoStack.Count > 0;
        HasRedoStack = _operationsRedoStack.Count > 0;
    }

    private void Flip(FlipDirection flipDirection)
    {
        string activityId = flipDirection switch
        {
            FlipDirection.Horizontal => ActivityIds.FlipHorizontal,
            FlipDirection.Vertical => ActivityIds.FlipVertical,
            _ => throw new InvalidOperationException("Unexpected FlipDirection value.")
        };

        TelemetryHelper.ExecuteActivity(_telemetryService, activityId, () =>
        {
            ImageOrientation oldOrientation = Orientation;
            ImageOrientation newOrientation = ImageOrientationHelper.GetFlippedOrientation(Orientation, flipDirection);
            Size imageSize = ImageOrientationHelper.GetOrientedImageSize(ImageSize, Orientation);
            Rectangle newCropRect = ImageOrientationHelper.GetFlippedCropRect(CropRect, imageSize, flipDirection);

            CropRect = newCropRect;
            Orientation = newOrientation;
            OrientationDisplayName = GetOrientationDisplayName(newOrientation);

            _operationsRedoStack.Clear();
            _operationsUndoStack.Push(new OrientationOperation(UpdateOrientationCommand, oldOrientation, newOrientation));
            UpdateUndoRedoStackProperties();
        });
    }

    private Task PrintAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Print, async () =>
        {
            nint hwnd = _appController.GetMainWindowHandle();
            await _imageCanvasPrinter.ShowPrintUIAsync([.. Drawables], GetImageCanvasRenderOptions(), hwnd);
        });
    }

    private Task ShareAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Share, async () =>
        {
            if (ImageFile == null)
            {
                throw new InvalidOperationException("No image to share");
            }

            nint hwnd = _appController.GetMainWindowHandle();
            await _shareService.ShareAsync(ImageFile.FilePath, hwnd);
        });
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
        OrientationDisplayName = GetOrientationDisplayName(newOrientation);
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCropRect(Rectangle newCropRect)
    {
        CropRect = newCropRect;
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string GetOrientationDisplayName(ImageOrientation orientation)
    {
        return orientation switch
        {
            ImageOrientation.RotateNoneFlipNone => "Normal",
            ImageOrientation.Rotate90FlipNone => "Rotated 90°",
            ImageOrientation.Rotate180FlipNone => "Rotated 180°",
            ImageOrientation.Rotate270FlipNone => "Rotated 270°",
            ImageOrientation.RotateNoneFlipX => "Flipped Horizontally",
            ImageOrientation.Rotate90FlipX => "Rotated 90° and Flipped Horizontally",
            ImageOrientation.Rotate180FlipX => "Rotated 180° and Flipped Horizontally",
            ImageOrientation.Rotate270FlipX => "Rotated 270° and Flipped Horizontally",
            _ => string.Empty,
        };
    }
}
