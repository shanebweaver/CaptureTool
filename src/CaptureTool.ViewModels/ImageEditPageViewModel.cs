using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.TaskEnvironment;
using CaptureTool.ViewModels.Commands;

namespace CaptureTool.ViewModels;

public sealed partial class ImageEditPageViewModel : ViewModelBase
{
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ICancellationService _cancellationService;

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

    public ImageEditPageViewModel(
        ITaskEnvironment taskEnvironment,
        ICancellationService cancellationService)
    {
        _taskEnvironment = taskEnvironment;
        _cancellationService = cancellationService;

        _drawables = [];
        _imageSize = new();
        _orientation = RotateFlipType.RotateNoneFlipNone;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

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
            Drawables.Add(new RectangleDrawable(new(50, 50), new(50, 50), Color.Red, 2));
            Drawables.Add(new TextDrawable(new(50, 50), "Hello world", Color.Yellow));
        }
        catch (OperationCanceledException)
        {
            // Load canceled
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        Drawables.Clear();
        base.Unload();
    }

    private void Copy()
    {
        if (ImageSize.Height > 0 && ImageSize.Width > 0)
        {
            ImageCanvasRenderOptions options = new(Orientation, ImageSize);
            _ = ImageCanvasRenderer.CopyImageToClipboardAsync([.. Drawables], options, ImageSize.Width, ImageSize.Height, 96);
        }
    }

    private void Crop()
    {

    }

    private void Save()
    {

    }

    private void Undo()
    {

    }

    private void Redo()
    {

    }

    private void Rotate()
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
    }

    bool IsTurned =>
        Orientation == RotateFlipType.Rotate90FlipNone ||
        Orientation == RotateFlipType.Rotate270FlipNone ||
        Orientation == RotateFlipType.Rotate90FlipX ||
        Orientation == RotateFlipType.Rotate270FlipX;

    private void Flip(bool isHorizontal)
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
            return;
        }

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

    private void Print()
    {

    }

    //public void ShowPrintUI()
    //{
    //    DispatcherQueue.TryEnqueue(async () =>
    //    {
    //        IDrawable[] toDraw = GetDrawablesToDraw();
    //        await ImageCanvasPrinter.ShowPrintUIAsync(toDraw);
    //    });
    //}

    //public void SaveImageToFile()
    //{
    //    FileSavePicker fileSavePicker = new FileSavePicker()
    //    { 
    //        SuggestedStartLocation = PickerLocationId.PicturesLibrary,
    //        SuggestedFileName = Path.GetFileName(ImageSource),
    //    };

    //    _ = fileSavePicker.PickSaveFileAsync();
    //}

    private static Size GetImageSize(string imagePath)
    {
        using FileStream file = new(imagePath, FileMode.Open, FileAccess.Read);
        var image = System.Drawing.Image.FromStream(
            stream: file,
            useEmbeddedColorManagement: false,
            validateImageData: false);

        float width = image.PhysicalDimension.Width;
        float height = image.PhysicalDimension.Height;
        return new(Convert.ToInt32(width), Convert.ToInt32(height));
    }
}
