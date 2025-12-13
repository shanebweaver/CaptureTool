using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.AddOns;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Store;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Windowing;
using CaptureTool.ViewModels.Helpers;
using static CaptureTool.Core.Interfaces.Store.CaptureToolStoreProducts;

namespace CaptureTool.ViewModels;

public sealed partial class AddOnsPageViewModel : AsyncLoadableViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadAddOnsPage";
        public static readonly string GetChromaKeyAddOn = "GetChromaKeyAddOn";
        public static readonly string GoBack = "GoBack";
    }

    private readonly IAddOnsActions _addOnsActions;
    private readonly IWindowHandleProvider _windowingService;
    private readonly IStoreService _storeService;
    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;

    public AsyncRelayCommand GetChromaKeyAddOnCommand { get; }
    public RelayCommand GoBackCommand { get; }

    public bool IsChromaKeyAddOnOwned
    {
        get => field;
        private set => Set(ref field, value);
    }

    public string ChromaKeyAddOnPrice
    {
        get => field;
        private set => Set(ref field, value);
    }

    public Uri? ChromaKeyAddOnLogoImage
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsChromaKeyAddOnAvailable
    {
        get => field;
        private set => Set(ref field, value);
    }

    public AddOnsPageViewModel(
        IAddOnsActions addOnsActions,
        IWindowHandleProvider windowingService,
        ILocalizationService localizationService,
        IStoreService storeService,
        ITelemetryService telemetryService,
        ICancellationService cancellationService)
    {
        _addOnsActions = addOnsActions;
        _windowingService = windowingService;
        _localizationService = localizationService;
        _storeService = storeService;
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;

        ChromaKeyAddOnPrice = localizationService.GetString("AddOns_ItemUnknown");

        GetChromaKeyAddOnCommand = new(GetChromaKeyAddOnAsync, () => IsChromaKeyAddOnAvailable);
        GoBackCommand = new(GoBack, () => _addOnsActions.CanGoBack());
    }

    public override Task LoadAsync(CancellationToken cancellationToken)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Load, async () =>
        {
            ThrowIfNotReadyToLoad();
            StartLoading();

            var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
            try
            {
                IStoreAddOn? addOn = await _storeService.GetAddonProductInfoAsync(AddOns.ChromaKeyBackgroundRemoval);
                if (addOn != null)
                {
                    bool isOwned = addOn.IsOwned;
                    IsChromaKeyAddOnAvailable = !isOwned;
                    IsChromaKeyAddOnOwned = isOwned;
                    ChromaKeyAddOnPrice = isOwned ? _localizationService.GetString("AddOns_ItemOwned") : addOn.Price;
                    ChromaKeyAddOnLogoImage = addOn.LogoImage;
                }
                else
                {
                    IsChromaKeyAddOnAvailable = false;
                    IsChromaKeyAddOnOwned = false;
                    ChromaKeyAddOnPrice = _localizationService.GetString("AddOns_ItemNotAvailable");
                    ChromaKeyAddOnLogoImage = null;
                }

                await base.LoadAsync(cancellationToken);
            }
            finally
            {
                cts.Dispose();
            }
        });
    }

    private Task GetChromaKeyAddOnAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.GetChromaKeyAddOn, async () =>
        {
            if (!IsChromaKeyAddOnOwned)
            {
                var hwnd = _windowingService.GetMainWindowHandle();
                bool success = await _storeService.PurchaseAddonAsync(AddOns.ChromaKeyBackgroundRemoval, hwnd);
                IsChromaKeyAddOnAvailable = !success;
                IsChromaKeyAddOnOwned = success;
                if (success)
                {
                    ChromaKeyAddOnPrice = _localizationService.GetString("AddOns_ItemOwned");
                }
            }
        });
    }

    private void GoBack()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.GoBack, () =>
        {
            _addOnsActions.GoBack();
        });
    }
}
