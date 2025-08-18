using CaptureTool.Services.Store;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockStoreService : IStoreService
{
    public readonly Dictionary<string, nint> _licenseCache = [];
    public const string DefaultPriceValue = "0";

    public void ClearLicenseCache()
    {
        _licenseCache.Clear();
    }

    public Task<StoreAddOn?> GetAddonProductInfoAsync(string storeProductId)
    {
        bool isOwned = _licenseCache.ContainsKey(storeProductId);
        StoreAddOn? result = isOwned ? new(storeProductId, isOwned, DefaultPriceValue, null) : null;
        return Task.FromResult(result);
    }

    public Task<bool> IsAddonPurchasedAsync(string storeProductId)
    {
        bool isPurchased = _licenseCache.ContainsKey(storeProductId);
        return Task.FromResult(isPurchased);
    }

    public Task<bool> PurchaseAddonAsync(string storeProductId, nint hwnd)
    {
        _licenseCache.Add(storeProductId, hwnd);
        return Task.FromResult(true);
    }
}
