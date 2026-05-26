using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Store;

internal class PurchaseChromaKeyAddOnAppCommand : IPurchaseChromaKeyAddOnAppCommand
{
    public PurchaseChromaKeyAddOnAppCommand(
        IStoreService storeService, 
        IWindowHandleProvider windowingService)
    {
        _storeService = storeService;
        _windowingService = windowingService;
    }

    private readonly IStoreService _storeService;
    private readonly IWindowHandleProvider _windowingService;

    public bool IsExecuting { get; protected set; }

    public bool CanExecute()
    {
        return !IsExecuting;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        IsExecuting = true;
        try
        {
            var hwnd = _windowingService.GetMainWindowHandle();
            bool success = await _storeService.PurchaseAddonAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, hwnd, cancellationToken);
            if (!success)
            {
                throw new Exception("Failed to purchase Chroma Key Background Removal add-on.");
            }
        }
        finally
        {
            IsExecuting = false;
        }
    }
}
