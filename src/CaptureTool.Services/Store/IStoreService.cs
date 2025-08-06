using System.Threading.Tasks;

namespace CaptureTool.Services.Store;

public interface IStoreService
{
    void ClearLicenseCache();
    Task<StoreAddOn?> GetAddonProductInfoAsync(string storeProductId);
    Task<bool> IsAddonPurchasedAsync(string storeProductId);
    Task<bool> PurchaseAddonAsync(string storeProductId);
}
