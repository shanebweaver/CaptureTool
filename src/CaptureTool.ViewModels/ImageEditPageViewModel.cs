using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Capture.Desktop.Annotation;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.TaskEnvironment;
using CaptureTool.ViewModels.Commands;

namespace CaptureTool.ViewModels;

public sealed partial class ImageEditPageViewModel : ViewModelBase
{
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ICancellationService _cancellationService;
    private readonly IFactoryService<ImageCanvasItemViewModel> _imageCanvasItemViewModelFactory;

    public RelayCommand CopyCommand => new(Copy);
    public RelayCommand CropCommand => new(Crop);
    public RelayCommand SaveCommand => new(Save);

    private ObservableCollection<CanvasItemViewModel> _canvasItems;
    public ObservableCollection<CanvasItemViewModel> CanvasItems
    {
        get => _canvasItems;
        set => Set(ref _canvasItems, value);
    }

    private ImageCanvasItemViewModel? _imageCanvasItemViewModel;

    public ImageEditPageViewModel(
        ITaskEnvironment taskEnvironment,
        ICancellationService cancellationService,
        IFactoryService<ImageCanvasItemViewModel> imageCanvasItemViewModelFactory)
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
                _imageCanvasItemViewModel = _imageCanvasItemViewModelFactory.Create();
                CanvasItems.Add(_imageCanvasItemViewModel);

                ImageAnnotationItem imageItem = new(imageFile, 0, 0);
                await _imageCanvasItemViewModel.LoadAsync(imageItem, cancellationToken);
            }

            RectangleShapeAnnotationItem rectangleItem = new(50, 50, 50, 50);
            _ = AddRectangleCanvasItemAsync(rectangleItem, cts.Token);

            TextAnnotationItem textItem = new("Hello world", 10, 10);
            _ = AddTextCanvasItemAsync(textItem, cts.Token);
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

    private async Task AddRectangleCanvasItemAsync(RectangleShapeAnnotationItem annotationItem, CancellationToken cancellationToken)
    {
        RectangleCanvasItemViewModel itemViewModel = new();
        CanvasItems.Add(itemViewModel);
        await itemViewModel.LoadAsync(annotationItem, cancellationToken);
    }

    private async Task AddTextCanvasItemAsync(TextAnnotationItem annotationItem, CancellationToken cancellationToken)
    {
        TextCanvasItemViewModel itemViewModel = new();
        CanvasItems.Add(itemViewModel);
        await itemViewModel.LoadAsync(annotationItem, cancellationToken);
    }

    public override void Unload()
    {
        CanvasItems.Clear();
        base.Unload();
    }

    private void Copy()
    {

    }

    private void Crop()
    {

    }

    private void Save()
    {

    }
}
