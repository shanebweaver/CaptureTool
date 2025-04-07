using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.TaskEnvironment;

namespace CaptureTool.ViewModels;

[WinRT.GeneratedBindableCustomProperty]
public sealed partial class ImageEditPageViewModel : ViewModelBase
{
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ICancellationService _cancellationService;
    private readonly IFactoryService<ImageCanvasItemViewModel> _imageCanvasItemViewModelFactory;

    private ObservableCollection<CanvasItemViewModel> _canvasItems;
    public ObservableCollection<CanvasItemViewModel> CanvasItems
    {
        get => _canvasItems;
        set => Set(ref _canvasItems, value);
    }

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
                ImageCanvasItemViewModel imageCanvasItemViewModel = _imageCanvasItemViewModelFactory.Create();
                CanvasItems.Add(imageCanvasItemViewModel);
                _ = imageCanvasItemViewModel.LoadAsync(imageFile, cts.Token);
            }
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
}
