using CaptureTool.Common.Commands;
using CaptureTool.Common.Storage;
using CaptureTool.Core.AppController;
using CaptureTool.Edit;
using CaptureTool.Edit.Drawable;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Storage;
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

    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly ITelemetryService _telemetryService;
    private readonly IImageCanvasPrinter _imageCanvasPrinter;
    private readonly IImageCanvasExporter _imageCanvasExporter;
    private readonly IFilePickerService _filePickerService;
    private readonly IFeatureManager _featureManager;

    public RelayCommand CopyCommand => new(Copy);
    public RelayCommand ToggleCropModeCommand => new(ToggleCropMode);
    public RelayCommand SaveCommand => new(Save);
    public RelayCommand UndoCommand => new(Undo);
    public RelayCommand RedoCommand => new(Redo);
    public RelayCommand RotateCommand => new(Rotate);
    public RelayCommand FlipHorizontalCommand => new(() => Flip(FlipDirection.Horizontal));
    public RelayCommand FlipVerticalCommand => new(() => Flip(FlipDirection.Vertical));
    public RelayCommand PrintCommand => new(Print);

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
        set => Set(ref _isInCropMode, value);
    }

    private Rectangle _cropRect;
    public Rectangle CropRect
    {
        get => _cropRect;
        set => Set(ref _cropRect, value);
    }

    private bool _isPrintEnabled;
    public bool IsPrintEnabled
    {
        get => _isPrintEnabled;
        set => Set(ref _isPrintEnabled, value);
    }

    private bool _isUndoRedoEnabled;
    public bool IsUndoRedoEnabled
    {
        get => _isUndoRedoEnabled;
        set => Set(ref _isUndoRedoEnabled, value);
    }

    public ImageEditPageViewModel(
        IAppController appController,
        ICancellationService cancellationService,
        ITelemetryService telemetryService,
        IImageCanvasPrinter imageCanvasPrinter,
        IImageCanvasExporter imageCanvasExporter,
        IFilePickerService filePickerService,
        IFeatureManager featureManager)
    {
        _appController = appController;
        _cancellationService = cancellationService;
        _telemetryService = telemetryService;
        _imageCanvasPrinter = imageCanvasPrinter;
        _filePickerService = filePickerService;
        _featureManager = featureManager;

        _drawables = [];
        _imageSize = new();
        _orientation = ImageOrientation.RotateNoneFlipNone;
        _cropRect = Rectangle.Empty;
        _imageCanvasExporter = imageCanvasExporter;
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
            IsPrintEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_Print);
            IsUndoRedoEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_UndoRedo);

            Vector2 topLeft = Vector2.Zero;
            if (parameter is ImageFile imageFile)
            {
                ImageFile = imageFile;
                ImageSize = _filePickerService.GetImageSize(imageFile);
                CropRect = new(Point.Empty, ImageSize);

                ImageDrawable imageDrawable = new(topLeft, imageFile);
                Drawables.Add(imageDrawable);
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
            ImageCanvasRenderOptions options = new(Orientation, ImageSize, CropRect);
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
                ImageCanvasRenderOptions options = new(Orientation, ImageSize, CropRect);
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

    private void Undo()
    {
        Trace.Assert(_featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_UndoRedo), "Print feature should be enabled before calling Undo.");

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
        Trace.Assert(_featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_UndoRedo), "Print feature should be enabled before calling Redo.");

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
        Trace.Assert(_featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_Print), "Print feature should be enabled before calling Print.");

        string activityId = ActivityIds.Print;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            nint hwnd = _appController.GetMainWindowHandle();
            await _imageCanvasPrinter.ShowPrintUIAsync([.. Drawables], new ImageCanvasRenderOptions(Orientation, ImageSize, CropRect), hwnd);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
