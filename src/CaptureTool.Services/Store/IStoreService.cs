using System.Threading.Tasks;

namespace CaptureTool.Services.Store;

public interface IStoreService
{
    void ClearLicenseCache();
    Task<bool> PurchaseAddonAsync(string storeProductId, nint hwnd);
    Task<StoreAddOn?> GetAddonProductInfoAsync(string storeProductId);
    Task<bool> IsAddonPurchasedAsync(string storeProductId);
}
