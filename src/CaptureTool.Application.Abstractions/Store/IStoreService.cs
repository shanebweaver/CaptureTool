namespace CaptureTool.Application.Abstractions.Store;

public interface IStoreService
{
    void ClearLicenseCache();
    Task<bool> LaunchAppStorePageAsync(CancellationToken cancellationToken);
    Task<bool> LaunchAppReviewAsync(CancellationToken cancellationToken);
    Task<bool> PurchaseAddonAsync(string storeProductId, CancellationToken cancellationToken);
    Task<IStoreAddOn> GetAddonProductInfoAsync(string storeProductId, CancellationToken cancellationToken);
    Task<bool> IsAddonPurchasedAsync(string storeProductId, CancellationToken cancellationToken);
}
