using CaptureTool.Infrastructure.Interfaces.Store;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using Windows.Services.Store;

namespace CaptureTool.Infrastructure.Implementations.Windows.Store;

public sealed partial class WindowsStoreService : IStoreService
{
    private readonly ITelemetryService _telemetryService;
    private readonly StoreContext _storeContext;
    private readonly Dictionary<string, StoreLicense> _licenseCache;

    public WindowsStoreService(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
        _storeContext = StoreContext.GetDefault();
        _licenseCache = [];
    }

    /// <summary>
    /// Checks if the specified add-on is purchased.
    /// </summary>
    public async Task<bool> IsAddonPurchasedAsync(string storeProductId)
    {
        string activityId = $"{nameof(WindowsStoreService)}.{nameof(IsAddonPurchasedAsync)}";
        try
        {
            _telemetryService.ActivityInitiated(activityId);

            if (_licenseCache.TryGetValue(storeProductId, out var cachedLicense))
            {
                return cachedLicense.IsActive;
            }

            var appLicense = await _storeContext.GetAppLicenseAsync();
            StoreLicense? addOnLicense = null;
            foreach (var licenseKvp in appLicense.AddOnLicenses)
            {
                // license keys from store context have extra data appended on the end.
                if (licenseKvp.Key.StartsWith(storeProductId) && licenseKvp.Value.IsActive)
                {
                    var licenseValue = licenseKvp.Value;
                    addOnLicense = licenseValue;
                    _licenseCache[storeProductId] = licenseValue;
                    return true;
                }
            }

            _telemetryService.ActivityCompleted(activityId);
            return false;
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
            return false;
        }
    }

    /// <summary>
    /// Prompts the user to purchase the specified add-on.
    /// Returns true if the purchase succeeded.
    /// </summary>
    public async Task<bool> PurchaseAddonAsync(string storeProductId, nint hwnd)
    {
        string activityId = $"{nameof(WindowsStoreService)}.{nameof(PurchaseAddonAsync)}";
        try
        {
            _telemetryService.ActivityInitiated(activityId);

            WinRT.Interop.InitializeWithWindow.Initialize(_storeContext, hwnd);
            StorePurchaseResult purchaseResult = await _storeContext.RequestPurchaseAsync(storeProductId);

            bool success = false;
            if (purchaseResult.Status == StorePurchaseStatus.Succeeded)
            {
                var appLicense = await _storeContext.GetAppLicenseAsync();
                if (appLicense.AddOnLicenses.TryGetValue(storeProductId, out var newLicense))
                {
                    _licenseCache[storeProductId] = newLicense;
                }
                success = true;
            }
            else if (purchaseResult.Status == StorePurchaseStatus.AlreadyPurchased)
            {
                success = true;
            }

            _telemetryService.ActivityCompleted(activityId);
            return success;
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
            return false;
        }
    }

    /// <summary>
    /// Gets the StoreProduct info for a given storeProductId (if available).
    /// </summary>
    public async Task<IStoreAddOn?> GetAddonProductInfoAsync(string storeProductId)
    {
        string activityId = $"{nameof(WindowsStoreService)}.{nameof(GetAddonProductInfoAsync)}";
        try
        {
            _telemetryService.ActivityInitiated(activityId);

            IList<string> productKinds = ["Durable"];
            IList<string> storeIds = [storeProductId];
            StoreProductQueryResult queryResult = await _storeContext.GetStoreProductsAsync(productKinds, storeIds);

            IStoreAddOn? addOn = null;
            if (queryResult.Products.TryGetValue(storeProductId, out var product))
            {
                StoreImage? logoImage = product.Images.Where(i => i.ImagePurposeTag == "Logo").FirstOrDefault();
                addOn = new WindowsStoreAddOn(product.InAppOfferToken, product.IsInUserCollection, product.Price.FormattedPrice, logoImage?.Uri);
            }

            _telemetryService.ActivityCompleted(activityId);
            return addOn;
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
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