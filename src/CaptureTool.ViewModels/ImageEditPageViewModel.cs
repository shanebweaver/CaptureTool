using CaptureTool.Capture.Desktop;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Telemetry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

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

    public RelayCommand CopyCommand => new(Copy);
    public RelayCommand ToggleCropModeCommand => new(ToggleCropMode);
    public RelayCommand SaveCommand => new(Save);
    public RelayCommand UndoCommand => new(Undo);
    public RelayCommand RedoCommand => new(Redo);
    public RelayCommand RotateCommand => new(Rotate);
    public RelayCommand FlipHorizontalCommand => new(() => Flip(true));
    public RelayCommand FlipVerticalCommand => new(() => Flip(false));
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

    private RotateFlipType _orientation;
    public RotateFlipType Orientation
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

    private Windows.Foundation.Rect _cropRect;
    public Windows.Foundation.Rect CropRect
    {
        get => _cropRect;
        set => Set(ref _cropRect, value);
    }

    private bool IsTurned =>
        Orientation == RotateFlipType.Rotate90FlipNone ||
        Orientation == RotateFlipType.Rotate270FlipNone ||
        Orientation == RotateFlipType.Rotate90FlipX ||
        Orientation == RotateFlipType.Rotate270FlipX;

    public ImageEditPageViewModel(
        IAppController appController,
        ICancellationService cancellationService,
        ITelemetryService telemetryService)
    {
        _appController = appController;
        _cancellationService = cancellationService;
        _telemetryService = telemetryService;

        _drawables = [];
        _imageSize = new();
        _orientation = RotateFlipType.RotateNoneFlipNone;
        _cropRect = new(0, 0, 0, 0);
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
                ImageSize = GetImageSize(imageFile.Path);
                CropRect = new(0, 0, ImageSize.Width, ImageSize.Height);

                ImageDrawable imageDrawable = new(topLeft, imageFile.Path);
                Drawables.Add(imageDrawable);
            }

            // Test drawables
            Drawables.Add(new RectangleDrawable(new(50, 50), new(50, 50), Color.Red, 2));
            Drawables.Add(new TextDrawable(new(50, 50), "Hello world", Color.Yellow));

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
            Orientation = RotateFlipType.RotateNoneFlipNone;
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
            await ImageCanvasRenderer.CopyImageToClipboardAsync([.. Drawables], options, ImageSize.Width, ImageSize.Height, 96);

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
            var filePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

#pragma warning disable IDE0028 // Simplify collection initialization
            filePicker.FileTypeChoices.Add("PNG", new List<string>() { ".png" });
#pragma warning restore IDE0028 // Simplify collection initialization

            nint hwnd = _appController.GetMainWindowHandle();
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            StorageFile file = await filePicker.PickSaveFileAsync();
            if (file != null)
            {
                ImageCanvasRenderOptions options = new(Orientation, ImageSize, CropRect);
                await ImageCanvasRenderer.SaveImageAsync(file.Path, [.. Drawables], options, ImageSize.Width, ImageSize.Height, 96);
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
            var newOrientation = Orientation switch
            {
                RotateFlipType.RotateNoneFlipNone => RotateFlipType.Rotate90FlipNone,
                RotateFlipType.Rotate90FlipNone => RotateFlipType.Rotate180FlipNone,
                RotateFlipType.Rotate180FlipNone => RotateFlipType.Rotate270FlipNone,
                RotateFlipType.Rotate270FlipNone => RotateFlipType.RotateNoneFlipNone,

                RotateFlipType.RotateNoneFlipX => RotateFlipType.Rotate90FlipX,
                RotateFlipType.Rotate90FlipX => RotateFlipType.Rotate180FlipX,
                RotateFlipType.Rotate180FlipX => RotateFlipType.Rotate270FlipX,
                RotateFlipType.Rotate270FlipX => RotateFlipType.RotateNoneFlipX,

                _ => throw new NotImplementedException("Unexpected RotateFlipType value"),
            };
            var newCropRect = UpdateCropRectOrientation(Orientation, newOrientation);

            Orientation = newOrientation;
            CropRect = newCropRect;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private Windows.Foundation.Rect UpdateCropRectOrientation(RotateFlipType oldOrientation, RotateFlipType newOrientation)
    {
        var oldRect = CropRect;
        var oldWidth = ImageSize.Width;
        var oldHeight = ImageSize.Height;

        // Determine rotation delta (in 90-degree steps, clockwise)
        int GetRotationSteps(RotateFlipType from, RotateFlipType to)
        {
            int[] angles = {
                0,   // RotateNoneFlipNone
                90,  // Rotate90FlipNone
                180, // Rotate180FlipNone
                270, // Rotate270FlipNone
                0,   // RotateNoneFlipX
                90,  // Rotate90FlipX
                180, // Rotate180FlipX
                270, // Rotate270FlipX
            };
            int fromIdx = (int)from % 8;
            int toIdx = (int)to % 8;
            int delta = (angles[toIdx] - angles[fromIdx] + 360) % 360;
            return delta / 90;
        }

        int steps = GetRotationSteps(oldOrientation, newOrientation) % 4;

        double x = oldRect.X, y = oldRect.Y, w = oldRect.Width, h = oldRect.Height;
        int width = oldWidth, height = oldHeight;

        if (steps == 1) // 90° CW
        {
            double newX = height - (y + h);
            double newY = x;
            double newW = h;
            double newH = w;
            x = newX;
            y = newY;
            w = newW;
            h = newH;
            int tmp = width;
            width = height;
            height = tmp;
        }
        else if (steps == 2) // 180°
        {
            double newX = width - (x + w);
            double newY = height - (y + h);
            x = newX;
            y = newY;
            // w and h stay the same
        }
        else if (steps == 3) // 270° CW (or 90° CCW)
        {
            double newX = y;
            double newY = width - (x + w);
            double newW = h;
            double newH = w;
            x = newX;
            y = newY;
            w = newW;
            h = newH;
            int tmp = width;
            width = height;
            height = tmp;
        }
        // steps == 0: no rotation

        // Clamp to new image bounds
        x = Math.Max(0, Math.Min(x, width - w));
        y = Math.Max(0, Math.Min(y, height - h));
        w = Math.Min(w, width - x);
        h = Math.Min(h, height - y);

        return new Windows.Foundation.Rect(x, y, w, h);
    }

    private void Flip(bool isHorizontal)
    {
        string activityId = isHorizontal ? ActivityIds.FlipHorizontal : ActivityIds.FlipVertical;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            if (IsTurned)
            {
                Orientation = Orientation switch
                {
                    RotateFlipType.RotateNoneFlipNone => isHorizontal ? RotateFlipType.RotateNoneFlipY : RotateFlipType.RotateNoneFlipX,
                    RotateFlipType.Rotate90FlipNone => isHorizontal ? RotateFlipType.Rotate90FlipY : RotateFlipType.Rotate90FlipX,
                    RotateFlipType.Rotate180FlipNone => isHorizontal ? RotateFlipType.Rotate180FlipY : RotateFlipType.Rotate180FlipX,
                    RotateFlipType.Rotate270FlipNone => isHorizontal ? RotateFlipType.Rotate270FlipY : RotateFlipType.Rotate270FlipX,

                    RotateFlipType.RotateNoneFlipY => isHorizontal ? RotateFlipType.RotateNoneFlipNone : RotateFlipType.RotateNoneFlipX,
                    RotateFlipType.Rotate90FlipY => isHorizontal ? RotateFlipType.Rotate90FlipNone : RotateFlipType.Rotate90FlipX,
                    RotateFlipType.Rotate180FlipY => isHorizontal ? RotateFlipType.Rotate180FlipNone : RotateFlipType.Rotate180FlipX,
                    RotateFlipType.Rotate270FlipY => isHorizontal ? RotateFlipType.Rotate270FlipNone : RotateFlipType.Rotate270FlipX,

                    _ => throw new NotImplementedException("Unexpected RotateFlipType value"),
                };
            }
            else
            {
                Orientation = Orientation switch
                {
                    RotateFlipType.RotateNoneFlipNone => isHorizontal ? RotateFlipType.RotateNoneFlipX : RotateFlipType.RotateNoneFlipY,
                    RotateFlipType.Rotate90FlipNone => isHorizontal ? RotateFlipType.Rotate90FlipX : RotateFlipType.Rotate90FlipY,
                    RotateFlipType.Rotate180FlipNone => isHorizontal ? RotateFlipType.Rotate180FlipX : RotateFlipType.Rotate180FlipY,
                    RotateFlipType.Rotate270FlipNone => isHorizontal ? RotateFlipType.Rotate270FlipX : RotateFlipType.Rotate270FlipY,

                    RotateFlipType.RotateNoneFlipX => isHorizontal ? RotateFlipType.RotateNoneFlipNone : RotateFlipType.Rotate180FlipNone,
                    RotateFlipType.Rotate90FlipX => isHorizontal ? RotateFlipType.Rotate90FlipNone : RotateFlipType.Rotate90FlipY,
                    RotateFlipType.Rotate180FlipX => isHorizontal ? RotateFlipType.Rotate180FlipNone : RotateFlipType.RotateNoneFlipNone,
                    RotateFlipType.Rotate270FlipX => isHorizontal ? RotateFlipType.Rotate270FlipNone : RotateFlipType.Rotate270FlipY,

                    _ => throw new NotImplementedException("Unexpected RotateFlipType value"),
                };
            }
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
            await ImageCanvasPrinter.ShowPrintUIAsync([.. Drawables], new ImageCanvasRenderOptions(Orientation, ImageSize, CropRect), hwnd);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private static Size GetImageSize(string imagePath)
    {
        using FileStream file = new(imagePath, FileMode.Open, FileAccess.Read);
        var image = Image.FromStream(
            stream: file,
            useEmbeddedColorManagement: false,
            validateImageData: false);

        float width = image.PhysicalDimension.Width;
        float height = image.PhysicalDimension.Height;
        return new(Convert.ToInt32(width), Convert.ToInt32(height));
    }
}
