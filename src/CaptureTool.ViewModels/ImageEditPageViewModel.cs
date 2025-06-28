using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Edit;
using CaptureTool.Edit.Drawable;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Telemetry;
using CaptureTool.Storage;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Orientation = CaptureTool.Edit.Orientation;

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

    private Orientation _orientation;
    public Orientation Orientation
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

    public ImageEditPageViewModel(
        IAppController appController,
        ICancellationService cancellationService,
        ITelemetryService telemetryService,
        IImageCanvasPrinter imageCanvasPrinter,
        IImageCanvasExporter imageCanvasExporter,
        IFilePickerService filePickerService)
    {
        _appController = appController;
        _cancellationService = cancellationService;
        _telemetryService = telemetryService;
        _imageCanvasPrinter = imageCanvasPrinter;
        _filePickerService = filePickerService;

        _drawables = [];
        _imageSize = new();
        _orientation = Orientation.RotateNoneFlipNone;
        _cropRect = new(0, 0, 0, 0);
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
            Vector2 topLeft = new(0, 0);
            if (parameter is ImageFile imageFile)
            {
                ImageFile = imageFile;
                ImageSize = _filePickerService.GetImageSize(imageFile);
                CropRect = new(0, 0, ImageSize.Width, ImageSize.Height);

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
            CropRect = new(0, 0, 0, 0);
            ImageSize = new(0, 0);
            Orientation = Orientation.RotateNoneFlipNone;
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
            Orientation oldOrientation = Orientation;
            Orientation newOrientation = OrientationHelper.GetRotatedOrientation(oldOrientation, RotationDirection.Clockwise);
            Rectangle newCropRect = OrientationHelper.GetOrientedCropRect(CropRect, ImageSize, oldOrientation, newOrientation);

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
            Size imageSize = OrientationHelper.GetOrientedImageSize(ImageSize, Orientation);
            CropRect = OrientationHelper.GetFlippedCropRect(CropRect, imageSize, flipDirection);
            Orientation = OrientationHelper.GetFlippedOrientation(Orientation, flipDirection);

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
            await _imageCanvasPrinter.ShowPrintUIAsync([.. Drawables], new ImageCanvasRenderOptions(Orientation, ImageSize, CropRect), hwnd);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);

            // Use error service to show an error message to the user
        }
    }
}
