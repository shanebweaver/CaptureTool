using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Features.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Store;

namespace CaptureTool.Application.Features.Store.PurchaseChromaKeyAddOn;

public sealed class PurchaseChromaKeyAddOnUseCase : IPurchaseChromaKeyAddOnUseCase
{
    private readonly IStoreService _storeService;

    public PurchaseChromaKeyAddOnUseCase(IStoreService storeService)
    {
        _storeService = storeService;
    }

    public bool CanExecute(PurchaseChromaKeyAddOnRequest request)
    {
        return true;
    }

    public async Task<PurchaseChromaKeyAddOnResponse> ExecuteAsync(PurchaseChromaKeyAddOnRequest request, CancellationToken cancellationToken = default)
    {
        bool success = await _storeService.PurchaseAddonAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
        if (!success)
        {
            throw new Exception("Failed to purchase Chroma Key Background Removal add-on.");
        }
        return new PurchaseChromaKeyAddOnResponse();
    }
}
