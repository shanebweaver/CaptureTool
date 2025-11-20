namespace CaptureTool.Services.Interfaces.Store;

public interface IStoreService
{
    void ClearLicenseCache();
    Task<bool> PurchaseAddonAsync(string storeProductId, nint hwnd);
    Task<IStoreAddOn?> GetAddonProductInfoAsync(string storeProductId);
    Task<bool> IsAddonPurchasedAsync(string storeProductId);
}
