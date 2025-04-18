using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Capture.Desktop.Annotation;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.TaskEnvironment;
using CaptureTool.ViewModels.Annotation;
using CaptureTool.ViewModels.Commands;
using Microsoft.Graphics.Canvas;

namespace CaptureTool.ViewModels;

public sealed partial class ImageEditPageViewModel : ViewModelBase
{
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ICancellationService _cancellationService;
    private readonly IFactoryService<ImageAnnotationViewModel> _imageCanvasItemViewModelFactory;

    public event EventHandler? CopyRequested;
    public event EventHandler? PrintRequested;

    public RelayCommand CopyCommand => new(Copy);
    public RelayCommand CropCommand => new(Crop);
    public RelayCommand SaveCommand => new(Save);
    public RelayCommand UndoCommand => new(Undo);
    public RelayCommand RedoCommand => new(Redo);
    public RelayCommand RotateCommand => new(Rotate);
    public RelayCommand PrintCommand => new(Print);

    // TODO: Add reference to a CanvasRenderTarget that we can manipulate here and then display in the UI.
    // We should handle the image manipulation upstream from the view to support various interactions with the image data.
    // Maybe wrap this up in a new class, ImageRenderer or something.
    private CanvasRenderTarget _canvasRenderTarget;

    private ObservableCollection<AnnotationItem> _canvasItems;
    public ObservableCollection<AnnotationItem> CanvasItems
    {
        get => _canvasItems;
        set => Set(ref _canvasItems, value);
    }

    private ImageFile? _imageFile;
    public ImageFile? ImageFile
    {
        get => _imageFile;
        set => Set(ref _imageFile, value);
    }

    public ImageAnnotationViewModel? ImageCanvasItemViewModel { get; private set; }

    public ImageEditPageViewModel(
        ITaskEnvironment taskEnvironment,
        ICancellationService cancellationService,
        IFactoryService<ImageAnnotationViewModel> imageCanvasItemViewModelFactory)
    {
        _taskEnvironment = taskEnvironment;
        _cancellationService = cancellationService;
        _imageCanvasItemViewModelFactory = imageCanvasItemViewModelFactory;

        _canvasItems = [];
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            if (parameter is ImageFile imageFile)
            {
                ImageFile = imageFile;
            }

            CanvasItems.Add(new RectangleShapeAnnotationItem(50, 50, 50, 50, Color.Red, 2));
            // TODO: Add an IImageCanvasCommand to a list as well to support undo/redo.

            CanvasItems.Add(new RectangleShapeAnnotationItem(-50, -50, 50, 50, Color.Blue, 4));
            CanvasItems.Add(new TextAnnotationItem("Hello world", 10, 10, Color.Yellow));
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
        CanvasItems.Clear();
        base.Unload();
    }

    private void Copy()
    {
        CopyRequested?.Invoke(this, EventArgs.Empty);
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
        PrintRequested?.Invoke(this, EventArgs.Empty);
    }
}
