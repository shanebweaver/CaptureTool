using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using Microsoft.UI;
using Windows.Foundation;

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

    public ImageEditPageViewModel(
        ITaskEnvironment taskEnvironment,
        ICancellationService cancellationService)
    {
        _taskEnvironment = taskEnvironment;
        _cancellationService = cancellationService;

        _drawables = [];
        _imageSize = new();
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
            Drawables.Add(new RectangleDrawable(new(50, 50), new(50, 50), Colors.Red, 2));
            Drawables.Add(new TextDrawable(new(50, 50), "Hello world", Colors.Yellow));
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
            _ = ImageCanvasRenderer.CopyImageToClipboardAsync([.. Drawables], (float)ImageSize.Width, (float)ImageSize.Height, 96);
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
        return new(width, height);
    }
}
