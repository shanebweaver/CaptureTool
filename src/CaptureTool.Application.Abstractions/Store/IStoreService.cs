namespace CaptureTool.Application.Abstractions.Store;

public interface IStoreService
{
    void ClearLicenseCache();
    Task<bool> PurchaseAddonAsync(string storeProductId, nint hwnd, CancellationToken cancellationToken);
    Task<IStoreAddOn> GetAddonProductInfoAsync(string storeProductId, CancellationToken cancellationToken);
    Task<bool> IsAddonPurchasedAsync(string storeProductId, CancellationToken cancellationToken);
}
