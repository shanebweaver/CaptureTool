using CaptureTool.Common.Commands;
using CaptureTool.Common.Storage;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Edit;
using CaptureTool.Edit.ChromaKey;
using CaptureTool.Edit.Drawable;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Store;
using CaptureTool.Services.Telemetry;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class ImageEditPageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "ImageEditPageViewModel_Load";
        public static readonly string Unload = "ImageEditPageViewModel_Unload";
        public static readonly string Copy = "ImageEditPageViewModel_Copy";
        public static readonly string ToggleCropMode = "ImageEditPageViewModel_ToggleCropMode";
        public static readonly string Save = "ImageEditPageViewModel_Save";
        public static readonly string Undo = "ImageEditPageViewModel_Undo";
        public static readonly string Redo = "ImageEditPageViewModel_Redo";
        public static readonly string Rotate = "ImageEditPageViewModel_Rotate";
        public static readonly string FlipHorizontal = "ImageEditPageViewModel_FlipHorizontal";
        public static readonly string FlipVertical = "ImageEditPageViewModel_FlipVertical";
        public static readonly string Print = "ImageEditPageViewModel_Print";
    }

    private readonly IStoreService _storeService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly ITelemetryService _telemetryService;
    private readonly IImageCanvasPrinter _imageCanvasPrinter;
    private readonly IImageCanvasExporter _imageCanvasExporter;
    private readonly IFilePickerService _filePickerService;

    private ImageDrawable? _imageDrawable;

    public event EventHandler? InvalidateCanvasRequested;

    public RelayCommand CopyCommand => new(Copy);
    public RelayCommand ToggleCropModeCommand => new(ToggleCropMode);
    public RelayCommand SaveCommand => new(Save);
    public RelayCommand UndoCommand => new(Undo, () => IsUndoRedoEnabled);
    public RelayCommand RedoCommand => new(Redo, () => IsUndoRedoEnabled);
    public RelayCommand RotateCommand => new(Rotate);
    public RelayCommand FlipHorizontalCommand => new(() => Flip(FlipDirection.Horizontal));
    public RelayCommand FlipVerticalCommand => new(() => Flip(FlipDirection.Vertical));
    public RelayCommand PrintCommand => new(Print);
    public RelayCommand<Color> UpdateChromaKeyColorCommand => new(UpdateChromaKeyColor, () => IsChromaKeyEnabled);

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

    public bool _isChromaKeyEnabled;
    public bool IsChromaKeyEnabled
    {
        get => _isChromaKeyEnabled;
        set => Set(ref _isChromaKeyEnabled, value);
    }

    public bool IsUndoRedoEnabled { get; }

    public ImageEditPageViewModel(
        IStoreService storeService,
        IAppController appController,
        ICancellationService cancellationService,
        ITelemetryService telemetryService,
        IImageCanvasPrinter imageCanvasPrinter,
        IImageCanvasExporter imageCanvasExporter,
        IFilePickerService filePickerService,
        IFeatureManager featureManager)
    {
        _storeService = storeService;
        _appController = appController;
        _cancellationService = cancellationService;
        _telemetryService = telemetryService;
        _imageCanvasPrinter = imageCanvasPrinter;
        _filePickerService = filePickerService;

        _drawables = [];
        _imageSize = new();
        _orientation = ImageOrientation.RotateNoneFlipNone;
        _cropRect = Rectangle.Empty;
        _imageCanvasExporter = imageCanvasExporter;
        _chromaKeyTolerance = 30;
        _chromaKeyColor = Color.Empty;
        _selectedChromaKeyColorOptionIndex = 0;
        _chromaKeyColorOptions = [];

        IsUndoRedoEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_UndoRedo);
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        string activityId = ActivityIds.Load;
        _telemetryService.ActivityInitiated(activityId);
        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            Vector2 topLeft = Vector2.Zero;
            if (parameter is ImageFile imageFile)
            {
                ImageFile = imageFile;
                ImageSize = _filePickerService.GetImageSize(imageFile);
                CropRect = new(Point.Empty, ImageSize);

                _imageDrawable = new(topLeft, imageFile, ImageSize);
                Drawables.Add(_imageDrawable);
            }

            bool isChromaKeyEnabled = await _storeService.IsAddonPurchasedAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval);
            IsChromaKeyEnabled = isChromaKeyEnabled;
            if (isChromaKeyEnabled)
            {
                ChromaKeyColorOptions.Add(ChromaKeyColorOption.Empty);
                foreach (var preset in ChromaKeyColorOptionPresets.All)
                {
                    ChromaKeyColorOptions.Add(preset);
                }
            }

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            _telemetryService.ActivityCanceled(activityId);
            throw;
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
            throw;
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        string activityId = ActivityIds.Unload;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            _imageDrawable = null;
            CropRect = Rectangle.Empty;
            ImageSize = Size.Empty;
            Orientation = ImageOrientation.RotateNoneFlipNone;
            Drawables.Clear();
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private async void Copy()
    {
        string activityId = ActivityIds.Copy;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
            await _imageCanvasExporter.CopyImageToClipboardAsync([.. Drawables], options);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void ToggleCropMode()
    {
        string activityId = ActivityIds.ToggleCropMode;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            IsInCropMode = !IsInCropMode;
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
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
        ChromaKeyColor = color;
    }

    private async void Save()
    {
        string activityId = ActivityIds.Save;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            nint hwnd = _appController.GetMainWindowHandle();
            var file = await _filePickerService.SaveImageFileAsync(hwnd);
            if (file != null)
            {
                ImageCanvasRenderOptions options = GetImageCanvasRenderOptions();
                await _imageCanvasExporter.SaveImageAsync(file.Path, [.. Drawables], options);
                _telemetryService.ActivityCompleted(activityId);
            }
            else
            {
                _telemetryService.ActivityCompleted(activityId, "User canceled");
            }
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    public ImageCanvasRenderOptions GetImageCanvasRenderOptions()
    {
        return new(Orientation, ImageSize, CropRect);
    }

    private void Undo()
    {
        string activityId = ActivityIds.Undo;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void Redo()
    {
        string activityId = ActivityIds.Redo;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void Rotate()
    {
        string activityId = ActivityIds.Rotate;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            ImageOrientation oldOrientation = Orientation;
            ImageOrientation newOrientation = ImageOrientationHelper.GetRotatedOrientation(oldOrientation, RotationDirection.Clockwise);
            Rectangle newCropRect = ImageOrientationHelper.GetOrientedCropRect(CropRect, ImageSize, oldOrientation, newOrientation);

            CropRect = newCropRect;
            Orientation = newOrientation;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void Flip(FlipDirection flipDirection)
    {
        string activityId = flipDirection switch
        {
            FlipDirection.Horizontal => ActivityIds.FlipHorizontal,
            FlipDirection.Vertical => ActivityIds.FlipVertical,
            _ => throw new InvalidOperationException("Unexpected FlipDirection value.")
        };
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            Size imageSize = ImageOrientationHelper.GetOrientedImageSize(ImageSize, Orientation);
            CropRect = ImageOrientationHelper.GetFlippedCropRect(CropRect, imageSize, flipDirection);
            Orientation = ImageOrientationHelper.GetFlippedOrientation(Orientation, flipDirection);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private async void Print()
    {
        string activityId = ActivityIds.Print;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            nint hwnd = _appController.GetMainWindowHandle();
            await _imageCanvasPrinter.ShowPrintUIAsync([.. Drawables], GetImageCanvasRenderOptions(), hwnd);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
