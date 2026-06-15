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
        try
        {
            bool success = await _storeService.PurchaseAddonAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
            return new PurchaseChromaKeyAddOnResponse(success);
        }
        catch (Exception)
        {
            return new PurchaseChromaKeyAddOnResponse(false);
        }
    }
}
