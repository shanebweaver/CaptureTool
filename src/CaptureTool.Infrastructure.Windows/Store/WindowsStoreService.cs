using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Windowing;
using Windows.Services.Store;
using Windows.System;

namespace CaptureTool.Infrastructure.Windows.Store;

public sealed partial class WindowsStoreService : IStoreService
{
    private static readonly Uri AppStorePageUri = new($"ms-windows-store://pdp/?ProductId={CaptureToolStoreProducts.AppProductId}");
    private static readonly Uri AppStoreReviewUri = new($"ms-windows-store://review/?ProductId={CaptureToolStoreProducts.AppProductId}");

    private readonly ILogService _logService;
    private readonly IWindowHandleProvider _windowHandleProvider;
    private readonly StoreContext _storeContext;
    private readonly Dictionary<string, StoreLicense> _licenseCache;
    private readonly ITelemetryService? _telemetryService;

    public WindowsStoreService(
        ILogService logService,
        IWindowHandleProvider windowHandleProvider,
        ITelemetryService? telemetryService = null)
    {
        _logService = logService;
        _windowHandleProvider = windowHandleProvider;
        _telemetryService = telemetryService;
        _storeContext = StoreContext.GetDefault();
        _licenseCache = [];
    }

    public Task<bool> LaunchAppStorePageAsync(CancellationToken cancellationToken)
    {
        return LaunchStoreUriAsync("LaunchAppStorePage", AppStorePageUri, cancellationToken);
    }

    public Task<bool> LaunchAppReviewAsync(CancellationToken cancellationToken)
    {
        return LaunchStoreUriAsync("LaunchAppReview", AppStoreReviewUri, cancellationToken);
    }

    /// <summary>
    /// Checks if the specified add-on is purchased.
    /// </summary>
    public async Task<bool> IsAddonPurchasedAsync(string storeProductId, CancellationToken cancellationToken)
    {
        string activityId = $"{nameof(WindowsStoreService)}.{nameof(IsAddonPurchasedAsync)}";
        try
        {
            _logService.LogInformation($"Activity initiated: {activityId}");

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

            _logService.LogInformation($"Activity completed: {activityId}");
            return false;
        }
        catch (Exception e)
        {
            _logService.LogException(e, $"Activity error: {activityId}");
            return false;
        }
    }

    /// <summary>
    /// Prompts the user to purchase the specified add-on.
    /// Returns true if the purchase succeeded.
    /// </summary>
    public async Task<bool> PurchaseAddonAsync(string storeProductId, CancellationToken cancellationToken)
    {
        string activityId = $"{nameof(WindowsStoreService)}.{nameof(PurchaseAddonAsync)}";
        string product = GetTelemetryProduct(storeProductId);
        _telemetryService?.TrackEvent(
            TelemetryEvents.StorePurchaseStarted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Product] = product
            });

        try
        {
            _logService.LogInformation($"Activity initiated: {activityId}");

            nint hwnd = _windowHandleProvider.GetMainWindowHandle();
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

            _logService.LogInformation($"Activity completed: {activityId}");
            TrackPurchaseCompleted(
                product,
                purchaseResult.Status.ToString(),
                success ? TelemetryOutcomes.Succeeded : TelemetryOutcomes.Failed);
            return success;
        }
        catch (OperationCanceledException e)
        {
            _logService.LogException(e, $"Activity canceled: {activityId}");
            TrackPurchaseCompleted(product, "canceled", TelemetryOutcomes.Canceled);
            return false;
        }
        catch (Exception e)
        {
            _logService.LogException(e, $"Activity error: {activityId}");
            TrackPurchaseCompleted(product, "exception", TelemetryOutcomes.Failed);
            return false;
        }
    }

    /// <summary>
    /// Gets the StoreProduct info for a given storeProductId (if available).
    /// </summary>
    public async Task<IStoreAddOn> GetAddonProductInfoAsync(string storeProductId, CancellationToken cancellationToken)
    {
        string activityId = $"{nameof(WindowsStoreService)}.{nameof(GetAddonProductInfoAsync)}";
        WindowsStoreAddOn addOn;
        try
        {
            _logService.LogInformation($"Activity initiated: {activityId}");

            IList<string> productKinds = ["Durable"];
            IList<string> storeIds = [storeProductId];
            StoreProductQueryResult queryResult = await _storeContext.GetStoreProductsAsync(productKinds, storeIds);

            if (queryResult.Products.TryGetValue(storeProductId, out var product))
            {
                StoreImage? logoImage = product.Images.Where(i => i.ImagePurposeTag == "Logo").FirstOrDefault();
                addOn = new WindowsStoreAddOn(product.InAppOfferToken, product.IsInUserCollection, product.Price.FormattedPrice, logoImage?.Uri);
            }
            else
            {
                throw new Exception($"Failed to get product info for {storeProductId}. Status: {queryResult.ExtendedError?.Message}");
            }
        }
        catch (Exception e)
        {
            _logService.LogException(e, $"Activity error: {activityId}");
            throw;
        }

        _logService.LogInformation($"Activity completed: {activityId}");
        return addOn;
    }

    /// <summary>
    /// Refreshes the license info cache manually.
    /// </summary>
    public void ClearLicenseCache()
    {
        _licenseCache.Clear();
    }

    private async Task<bool> LaunchStoreUriAsync(string activityName, Uri uri, CancellationToken cancellationToken)
    {
        string activityId = $"{nameof(WindowsStoreService)}.{activityName}";
        try
        {
            _logService.LogInformation($"Activity initiated: {activityId}");

            cancellationToken.ThrowIfCancellationRequested();
            bool launched = await Launcher.LaunchUriAsync(uri);
            cancellationToken.ThrowIfCancellationRequested();

            _logService.LogInformation($"Activity completed: {activityId}");
            TrackStoreOpened(activityName, launched);
            return launched;
        }
        catch (Exception e)
        {
            _logService.LogException(e, $"Activity error: {activityId}");
            TrackStoreOpened(activityName, false);
            return false;
        }
    }

    private void TrackPurchaseCompleted(string product, string status, string outcome)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.StorePurchaseCompleted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Product] = product,
                [TelemetryProperties.Status] = status,
                [TelemetryProperties.Outcome] = outcome
            });
    }

    private void TrackStoreOpened(string activityName, bool launched)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.StoreOpened,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Operation] = activityName == "LaunchAppReview"
                    ? "review"
                    : "app_page",
                [TelemetryProperties.Outcome] = launched
                    ? TelemetryOutcomes.Succeeded
                    : TelemetryOutcomes.Failed
            });
    }

    private static string GetTelemetryProduct(string storeProductId)
    {
        return storeProductId == CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval
            ? "chroma_key_background_removal"
            : "other";
    }
}
