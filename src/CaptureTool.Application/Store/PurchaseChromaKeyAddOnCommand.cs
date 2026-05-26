using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Store;

internal class PurchaseChromaKeyAddOnCommand : IPurchaseChromaKeyAddOnCommand
{
    public PurchaseChromaKeyAddOnCommand(
        IStoreService storeService, 
        IWindowHandleProvider windowingService)
    {
        _storeService = storeService;
        _windowingService = windowingService;
    }

    private readonly IStoreService _storeService;
    private readonly IWindowHandleProvider _windowingService;

    public bool IsExecuting { get; protected set; }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return !IsExecuting;
    }

    public async Task ExecuteAsync()
    {
        IsExecuting = true;
        try
        {
            var hwnd = _windowingService.GetMainWindowHandle();
            bool success = await _storeService.PurchaseAddonAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, hwnd);
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
