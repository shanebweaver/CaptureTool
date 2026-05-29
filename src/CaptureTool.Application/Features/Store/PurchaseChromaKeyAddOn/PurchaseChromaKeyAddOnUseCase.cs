using CaptureTool.Application.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Features.Store.PurchaseChromaKeyAddOn;

public sealed class PurchaseChromaKeyAddOnUseCase : IUseCase<PurchaseChromaKeyAddOnRequest, PurchaseChromaKeyAddOnResponse>, IConditional<PurchaseChromaKeyAddOnRequest>
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

    public Task<bool> CanExecuteAsync(PurchaseChromaKeyAddOnRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
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