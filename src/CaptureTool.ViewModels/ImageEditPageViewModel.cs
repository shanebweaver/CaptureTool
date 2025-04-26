using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Core;
using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Telemetry;
using CaptureTool.ViewModels.Commands;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CaptureTool.ViewModels;

public sealed partial class ImageEditPageViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "Load";
        public static readonly string Unload = "Unload";
        public static readonly string Copy = "Copy";
        public static readonly string Crop = "Crop";
        public static readonly string Save = "Save";
        public static readonly string Undo = "Undo";
        public static readonly string Redo = "Redo";
        public static readonly string Rotate = "Rotate";
        public static readonly string FlipHorizontal = "FlipHorizontal";
        public static readonly string FlipVertical = "FlipVertical";
        public static readonly string Print = "Print";
    }

    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand CopyCommand => new(Copy);
    public RelayCommand CropCommand => new(Crop);
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

                ImageDrawable imageDrawable = new(topLeft, imageFile.Path);
                Drawables.Add(imageDrawable);
            }

            // Test drawables
            //Drawables.Add(new RectangleDrawable(new(50, 50), new(50, 50), Color.Red, 2));
            //Drawables.Add(new TextDrawable(new(50, 50), "Hello world", Color.Yellow));

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            _telemetryService.ActivityCanceled(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
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
            ImageCanvasRenderOptions options = new(Orientation, ImageSize);
            await ImageCanvasRenderer.CopyImageToClipboardAsync([.. Drawables], options, ImageSize.Width, ImageSize.Height, 96);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void Crop()
    {
        string activityId = ActivityIds.Crop;
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
            filePicker.FileTypeChoices.Add("PNG", [".png"]);

            nint hwnd = _appController.GetMainWindowHandle();
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            StorageFile file = await filePicker.PickSaveFileAsync();
            if (file != null)
            {
                ImageCanvasRenderOptions options = new(Orientation, ImageSize);
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
            Orientation = Orientation switch
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
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
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

                    RotateFlipType.RotateNoneFlipX => isHorizontal ? RotateFlipType.RotateNoneFlipNone : RotateFlipType.RotateNoneFlipY,
                    RotateFlipType.Rotate90FlipX => isHorizontal ? RotateFlipType.Rotate90FlipNone : RotateFlipType.Rotate90FlipY,
                    RotateFlipType.Rotate180FlipX => isHorizontal ? RotateFlipType.Rotate180FlipNone : RotateFlipType.Rotate180FlipY,
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

    private void Print()
    {
        string activityId = ActivityIds.Print;
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
