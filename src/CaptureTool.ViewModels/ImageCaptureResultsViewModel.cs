using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Cancellation;
using Windows.Storage;

namespace CaptureTool.ViewModels;

public class ImageCaptureResultsViewModel : ViewModelBase
{
    private readonly ICancellationService _cancellationService;

    private IStorageFile? _imageFile;
    public IStorageFile? ImageFile
    {
        get => _imageFile;
        set => Set(ref _imageFile, value);
    }

    public ImageCaptureResultsViewModel(
        ICancellationService cancellationService)
    {
        _cancellationService = cancellationService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            // Load here
            if (parameter is IStorageFile imageFile)
            {
                ImageFile = imageFile;
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
        _imageFile = null;
        base.Unload();
    }
}
