using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Store;

namespace CaptureTool.Application.Store;

public class GetChromaKeyAddOnQuery : IGetChromaKeyAddOnQuery
{
    public GetChromaKeyAddOnQuery(IStoreService storeService)
    {
        _storeService = storeService;
    }

    private readonly IStoreService _storeService;

    public bool IsExecuting { get; protected set; }

    public event EventHandler? CanExecuteChanged;

    public async Task<IStoreAddOn> ExecuteAsync()
    {
        IsExecuting = true;
        try
        {
            return await _storeService.GetAddonProductInfoAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval);
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
