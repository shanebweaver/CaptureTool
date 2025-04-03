using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Cancellation;

namespace CaptureTool.ViewModels;

public class AudioCaptureResultsViewModel : ViewModelBase
{
    private readonly ICancellationService _cancellationService;

    public AudioCaptureResultsViewModel(
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
        base.Unload();
    }
}
