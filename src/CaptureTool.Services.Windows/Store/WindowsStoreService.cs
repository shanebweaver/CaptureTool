using CaptureTool.Services.Store;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace CaptureTool.Services.Windows.Store;

public sealed partial class WindowsStoreService : IStoreService
{
    private readonly StoreContext _storeContext;
    private readonly Dictionary<string, StoreLicense> _licenseCache;

    public WindowsStoreService()
    {
        _storeContext = StoreContext.GetDefault();
        _licenseCache = [];
    }

    /// <summary>
    /// Checks if the specified add-on is purchased.
    /// </summary>
    public async Task<bool> IsAddonPurchasedAsync(string storeProductId)
    {
        try
        {
            if (_licenseCache.TryGetValue(storeProductId, out var cachedLicense))
            {
                return cachedLicense.IsActive;
            }

            var appLicense = await _storeContext.GetAppLicenseAsync();
            StoreLicense? addOnLicense = null;
            foreach (var licenseKvp in appLicense.AddOnLicenses)
            {
                // license keys from store context have extra data appended on the end.
                if (licenseKvp.Key.StartsWith(storeProductId))
                {
                    var licenseValue = licenseKvp.Value;
                    addOnLicense = licenseValue;
                    _licenseCache[storeProductId] = licenseValue;
                    return licenseValue.IsActive;
                }
            }

            return false;
        }
        catch (Exception)
        {
            // TODO: Log error
            return false;
        }
    }

    /// <summary>
    /// Prompts the user to purchase the specified add-on.
    /// Returns true if the purchase succeeded.
    /// </summary>
    public async Task<bool> PurchaseAddonAsync(string storeProductId, nint hwnd)
    {
        try
        {
            WinRT.Interop.InitializeWithWindow.Initialize(_storeContext, hwnd);
            var result = await _storeContext.RequestPurchaseAsync(storeProductId);

            switch (result.Status)
            {
                case StorePurchaseStatus.Succeeded:
                    var appLicense = await _storeContext.GetAppLicenseAsync();
                    if (appLicense.AddOnLicenses.TryGetValue(storeProductId, out var newLicense))
                    {
                        _licenseCache[storeProductId] = newLicense;
                        return true;
                    }
                    return false;
                case StorePurchaseStatus.AlreadyPurchased:
                    return true;
                case StorePurchaseStatus.NotPurchased:
                    // User cancelled
                    return false;
                default:
                    // Network error or unknown error
                    return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the StoreProduct info for a given storeProductId (if available).
    /// </summary>
    public async Task<StoreAddOn?> GetAddonProductInfoAsync(string storeProductId)
    {
        try
        {
            IList<string> productKinds = [ "Durable" ];
            IList<string> storeIds = [ storeProductId ];
            var result = await _storeContext.GetStoreProductsAsync(productKinds, storeIds);
            if (result.Products.TryGetValue(storeProductId, out var product))
            {
                StoreAddOn addOn = new(product.InAppOfferToken, product.IsInUserCollection, product.Price.FormattedPrice);
                return addOn;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Refreshes the license info cache manually.
    /// </summary>
    public void ClearLicenseCache()
    {
        _licenseCache.Clear();
    }
}