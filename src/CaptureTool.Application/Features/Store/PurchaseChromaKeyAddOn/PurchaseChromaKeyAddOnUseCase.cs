using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Features.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Windowing;

namespace CaptureTool.Application.Features.Store.PurchaseChromaKeyAddOn;

public sealed class PurchaseChromaKeyAddOnUseCase : IPurchaseChromaKeyAddOnUseCase
{
    private readonly IStoreService _storeService;
    private readonly IWindowHandleProvider _windowingService;

    public PurchaseChromaKeyAddOnUseCase(
        IStoreService storeService,
        IWindowHandleProvider windowingService)
    {
        _storeService = storeService;
        _windowingService = windowingService;
    }

    public bool CanExecute(PurchaseChromaKeyAddOnRequest request)
    {
        return true;
    }

    public async Task<PurchaseChromaKeyAddOnResponse> ExecuteAsync(PurchaseChromaKeyAddOnRequest request, CancellationToken cancellationToken = default)
    {
        var hwnd = _windowingService.GetMainWindowHandle();
        bool success = await _storeService.PurchaseAddonAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, hwnd, cancellationToken);
        if (!success)
        {
            throw new Exception("Failed to purchase Chroma Key Background Removal add-on.");
        }
        return new PurchaseChromaKeyAddOnResponse();
    }
}
