using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Cancellation;
using Windows.Storage;

namespace CaptureTool.ViewModels;

public class ImageEditPageViewModel : ViewModelBase
{
    private readonly ICancellationService _cancellationService;

    private IStorageFile? _imageFile;
    public IStorageFile? ImageFile
    {
        get => _imageFile;
        set => Set(ref _imageFile, value);
    }

    public ImageEditPageViewModel(
        ICancellationService cancellationService)
    {
        _cancellationService = cancellationService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
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
        ImageFile = null;
        base.Unload();
    }
}
