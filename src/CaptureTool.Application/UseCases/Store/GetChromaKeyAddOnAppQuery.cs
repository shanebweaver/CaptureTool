using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Store;

namespace CaptureTool.Application.UseCases.Store;

internal class GetChromaKeyAddOnAppQuery : IGetChromaKeyAddOnAppQuery
{
    public GetChromaKeyAddOnAppQuery(IStoreService storeService)
    {
        _storeService = storeService;
    }

    private readonly IStoreService _storeService;

    public bool IsExecuting { get; protected set; }

    public async Task<IStoreAddOn> ExecuteAsync(CancellationToken cancellationToken)
    {
        IsExecuting = true;
        try
        {
            return await _storeService.GetAddonProductInfoAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
        }
        finally 
        { 
            IsExecuting = false; 
        }
    }

    public bool CanExecute()
    {
        return true;
    }
}
