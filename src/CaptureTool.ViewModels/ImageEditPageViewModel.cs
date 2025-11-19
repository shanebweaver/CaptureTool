using CaptureTool.Common.Commands;
using CaptureTool.Common.Storage;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Edit;
using CaptureTool.Edit.ChromaKey;
using CaptureTool.Edit.Drawable;
using CaptureTool.Edit.Operations;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Share;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Store;
using CaptureTool.Services.Telemetry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class ImageEditPageViewModel : AsyncLoadableViewModelBase<ImageFile>
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "ImageEditPageViewModel_Load";
        public static readonly string Dispose = "ImageEditPageViewModel_Dispose";
        public static readonly string Copy = "ImageEditPageViewModel_Copy";
        public static readonly string ToggleCropMode = "ImageEditPageViewModel_ToggleCropMode";
        public static readonly string Save = "ImageEditPageViewModel_Save";
        public static readonly string Undo = "ImageEditPageViewModel_Undo";
        public static readonly string Redo = "ImageEditPageViewModel_Redo";
        public static readonly string Rotate = "ImageEditPageViewModel_Rotate";
        public static readonly string FlipHorizontal = "ImageEditPageViewModel_FlipHorizontal";
        public static readonly string FlipVertical = "ImageEditPageViewModel_FlipVertical";
        public static readonly string Print = "ImageEditPageViewModel_Print";
        public static readonly string Share = "ImageEditPageViewModel_Share";
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

    public RelayCommand CopyCommand { get; }
    public RelayCommand ToggleCropModeCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand RotateCommand { get; }
    public RelayCommand FlipHorizontalCommand { get; }
    public RelayCommand FlipVerticalCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand ShareCommand { get; }
    public RelayCommand<Color> UpdateChromaKeyColorCommand { get; }

    // Private commands to handle undo/redo operations.
    private RelayCommand<ImageOrientation> UpdateOrientationCommand { get; }
    private RelayCommand<Rectangle> UpdateCropRectCommand { get; }


    private bool _hasUndoStack;
    public bool HasUndoStack
    {
        get => _hasUndoStack;
        set => Set(ref _hasUndoStack, value);
    }

    private bool _hasRedoStack;
    public bool HasRedoStack
    {
        get => _hasRedoStack;
        set => Set(ref _hasRedoStack, value);
    }

    private ObservableCollection<IDrawable> _drawables;
    public ObservableCollection<IDrawable> Drawables
    {
        get => _drawables;
        set => Set(ref _drawables, value);
    }

    private ImageFile? _imageFile;
    public ImageFile? ImageFile
    {
        get => _imageFile;
        set => Set(ref _imageFile, value);
    }

    private Size _imageSize;
    public Size ImageSize
    {
        get => _imageSize;
        set => Set(ref _imageSize, value);
    }

    private ImageOrientation _orientation;
    public ImageOrientation Orientation
    {
        get => _orientation;
        set => Set(ref _orientation, value);
    }

    private bool _isInCropMode;
    public bool IsInCropMode
    {
        get => _isInCropMode;
        set
        {
            Set(ref _isInCropMode, value);
            if (value && _showChromaKeyOptions)
            {
                ShowChromaKeyOptions = false;
            }
        }
    }

    private Rectangle _cropRect;
    public Rectangle CropRect
    {
        get => _cropRect;
        set => Set(ref _cropRect, value);
    }

    private bool _showChromaKeyOptions;
    public bool ShowChromaKeyOptions
    {
        get => _showChromaKeyOptions;
        set
        {
            Set(ref _showChromaKeyOptions, value);
            if (value && _isInCropMode)
            {
                IsInCropMode = false;
            }
        }
    }

    private int _chromaKeyTolerance;
    public int ChromaKeyTolerance
    {
        get => _chromaKeyTolerance;
        set
        {
            Set(ref _chromaKeyTolerance, value);
            UpdateChromaKeyEffectValues();
        }
    }

    private int _chromaKeyDesaturation;
    public int ChromaKeyDesaturation
    {
        get => _chromaKeyDesaturation;
        set
        {
            Set(ref _chromaKeyDesaturation, value);
            UpdateChromaKeyEffectValues();
        }
    }

    private Color _chromaKeyColor;
    public Color ChromaKeyColor
    {
        get => _chromaKeyColor;
        set
        {
            Set(ref _chromaKeyColor, value);
            UpdateChromaKeyEffectValues();
        }
    }

    private ObservableCollection<ChromaKeyColorOption> _chromaKeyColorOptions;
    public ObservableCollection<ChromaKeyColorOption> ChromaKeyColorOptions
    {
        get => _chromaKeyColorOptions;
        set => Set(ref _chromaKeyColorOptions, value);
    }

    private int _selectedChromaKeyColorOptionIndex;
    public int SelectedChromaKeyColorOption
    {
        get => _selectedChromaKeyColorOptionIndex;
        set
        {
            Set(ref _selectedChromaKeyColorOptionIndex, value);
            UpdateChromaKeyColor(_chromaKeyColorOptions[value].Color);
        }
    }

    public bool _isChromaKeyAddOnOwned;
    public bool IsChromaKeyAddOnOwned
    {
        get => _isChromaKeyAddOnOwned;
        set => Set(ref _isChromaKeyAddOnOwned, value);
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

        _drawables = [];
        _imageSize = new();
        _orientation = ImageOrientation.RotateNoneFlipNone;
        _cropRect = Rectangle.Empty;
        _imageCanvasExporter = imageCanvasExporter;
        _chromaKeyTolerance = 30;
        _chromaKeyColor = Color.Empty;
        _selectedChromaKeyColorOptionIndex = 0;
        _chromaKeyColorOptions = [];
        _operationsUndoStack = [];
        _operationsRedoStack = [];

        CopyCommand = new RelayCommand(Copy);
        ToggleCropModeCommand = new RelayCommand(ToggleCropMode);
        SaveCommand = new RelayCommand(Save);
        UndoCommand = new RelayCommand(Undo);
        RedoCommand = new RelayCommand(Redo);
        RotateCommand = new RelayCommand(Rotate);
        FlipHorizontalCommand = new RelayCommand(() => Flip(FlipDirection.Horizontal));
        FlipVerticalCommand = new RelayCommand(() => Flip(FlipDirection.Vertical));
        PrintCommand = new RelayCommand(Print);
        ShareCommand = new RelayCommand(Share);
        UpdateChromaKeyColorCommand = new RelayCommand<Color>(UpdateChromaKeyColor, () => _featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey));
        UpdateOrientationCommand = new RelayCommand<ImageOrientation>(UpdateOrientation);
        UpdateCropRectCommand = new RelayCommand<Rectangle>(UpdateCropRect);
    }

    public override Task LoadAsync(ImageFile imageFile, CancellationToken cancellationToken)
    {
        return TelemetryHelpers.ExecuteActivityAsync(_telemetryService, ActivityIds.Load, async () =>
        {
            var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
            try
            {
                Vector2 topLeft = Vector2.Zero;
                ImageFile = imageFile;
                ImageSize = _filePickerService.GetImageSize(imageFile);
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
        Drawables.Clear();
        base.Dispose();
    }

    private void Copy()
    {
        TelemetryHelpers.ExecuteActivity(_telemetryService, ActivityIds.Copy, async () =>
        {
            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            await _imageCanvasExporter.CopyImageToClipboardAsync([.. Drawables], options);
        });
    }

    private void ToggleCropMode()
    {
        TelemetryHelpers.ExecuteActivity(_telemetryService, ActivityIds.ToggleCropMode, async () =>
        {
            IsInCropMode = !IsInCropMode;
        });
    }

    private void UpdateChromaKeyEffectValues()
    {
        if (_imageDrawable != null && _imageDrawable.ImageEffect == null)
        {
            _imageDrawable.ImageEffect = new ImageChromaKeyEffect(ChromaKeyColor, ChromaKeyTolerance / 100f, ChromaKeyDesaturation / 100f)
            {
                IsEnabled = !_chromaKeyColor.IsEmpty
            };
        }
        else if (_imageDrawable?.ImageEffect is ImageChromaKeyEffect chromaKeyEffect)
        {
            chromaKeyEffect.Tolerance = ChromaKeyTolerance / 100f;
            chromaKeyEffect.Desaturation = ChromaKeyDesaturation / 100f;
            chromaKeyEffect.Color = ChromaKeyColor;
            chromaKeyEffect.IsEnabled = !_chromaKeyColor.IsEmpty;
        }

        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateChromaKeyColor(Color color)
    {
        if (!_featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey))
        {
            return;
        }

        ChromaKeyColor = color;
    }

    private void Save()
    {
        TelemetryHelpers.ExecuteActivity(_telemetryService, ActivityIds.Save, async () =>
        {
            nint hwnd = _appController.GetMainWindowHandle();
            var file = await _filePickerService.SaveImageFileAsync(hwnd);
            if (file != null)
            {
                ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
                await _imageCanvasExporter.SaveImageAsync(file.Path, [.. Drawables], options);
            }
        });
    }

    public ImageCanvasRenderOptions GetImageCanvasRenderOptions()
    {
        return new(Orientation, ImageSize, CropRect);
    }

    private void Undo()
    {
        TelemetryHelpers.ExecuteActivity(_telemetryService, ActivityIds.Undo, async () =>
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
        TelemetryHelpers.ExecuteActivity(_telemetryService, ActivityIds.Redo, async () =>
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
        TelemetryHelpers.ExecuteActivity(_telemetryService, ActivityIds.Rotate, async () =>
        {
            ImageOrientation oldOrientation = Orientation;
            ImageOrientation newOrientation = ImageOrientationHelper.GetRotatedOrientation(oldOrientation, RotationDirection.Clockwise);
            Rectangle newCropRect = ImageOrientationHelper.GetOrientedCropRect(CropRect, ImageSize, oldOrientation, newOrientation);

            CropRect = newCropRect;
            Orientation = newOrientation;

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

        TelemetryHelpers.ExecuteActivity(_telemetryService, activityId, async () =>
        {
            ImageOrientation oldOrientation = Orientation;
            ImageOrientation newOrientation = ImageOrientationHelper.GetFlippedOrientation(Orientation, flipDirection);
            Size imageSize = ImageOrientationHelper.GetOrientedImageSize(ImageSize, Orientation);
            Rectangle newCropRect = ImageOrientationHelper.GetFlippedCropRect(CropRect, imageSize, flipDirection);

            CropRect = newCropRect;
            Orientation = newOrientation;

            _operationsRedoStack.Clear();
            _operationsUndoStack.Push(new OrientationOperation(UpdateOrientationCommand, oldOrientation, newOrientation));
            UpdateUndoRedoStackProperties();
        });
    }

    private async void Print()
    {
        TelemetryHelpers.ExecuteActivity(_telemetryService, ActivityIds.Print, async () =>
        {
            nint hwnd = _appController.GetMainWindowHandle();
            await _imageCanvasPrinter.ShowPrintUIAsync([.. Drawables], GetImageCanvasRenderOptions(), hwnd);
        });
    }

    private async void Share()
    {
        TelemetryHelpers.ExecuteActivity(_telemetryService, ActivityIds.Share, async () =>
        {
            if (_imageFile == null)
            {
                throw new InvalidOperationException("No image to share");
            }

            nint hwnd = _appController.GetMainWindowHandle();
            await _shareService.ShareAsync(_imageFile.Path, hwnd);
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
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCropRect(Rectangle newCropRect)
    {
        CropRect = newCropRect;
        InvalidateCanvasRequested?.Invoke(this, EventArgs.Empty);
    }
}
