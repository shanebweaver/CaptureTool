using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Telemetry;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Store;
using CaptureTool.Services.Telemetry;
using System;
using System.Threading;
using System.Threading.Tasks;
using static CaptureTool.Core.Store.CaptureToolStoreProducts;

namespace CaptureTool.ViewModels;

public sealed partial class AddOnsPageViewModel : AsyncLoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "AddOnsPageViewModel_Load";
        public static readonly string GetChromaKeyAddOn = "AddOnsPageViewModel_GetChromaKeyAddOn";
        public static readonly string GoBack = "AddOnsPageViewModel_GoBack";
    }

    private readonly IAppNavigation _appNavigation;
    private readonly IAppController _appController;
    private readonly IStoreService _storeService;
    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;

    public RelayCommand GetChromaKeyAddOnCommand { get; }
    public RelayCommand GoBackCommand { get; }

    private bool _isChromaKeyAddOnOwned;
    public bool IsChromaKeyAddOnOwned
    {
        get => _isChromaKeyAddOnOwned;
        private set => Set(ref _isChromaKeyAddOnOwned, value);
    }

    private string _chromaKeyAddOnPrice = string.Empty;
    public string ChromaKeyAddOnPrice
    {
        get => _chromaKeyAddOnPrice;
        private set => Set(ref _chromaKeyAddOnPrice, value);
    }

    private Uri? _chromaKeyAddOnLogoImage;
    public Uri? ChromaKeyAddOnLogoImage
    {
        get => _chromaKeyAddOnLogoImage;
        private set => Set(ref _chromaKeyAddOnLogoImage, value);
    }

    private bool _isChromaKeyAddOnAvailable;
    public bool IsChromaKeyAddOnAvailable
    {
        get => _isChromaKeyAddOnAvailable;
        private set => Set(ref _isChromaKeyAddOnAvailable, value);
    }

    public AddOnsPageViewModel(
        IAppNavigation appNavigation,
        IAppController appController,
        ILocalizationService localizationService,
        IStoreService storeService,
        ITelemetryService telemetryService,
        ICancellationService cancellationService)
    {
        _appNavigation = appNavigation;
        _appController = appController;
        _localizationService = localizationService;
        _storeService = storeService;
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;

        _chromaKeyAddOnPrice = localizationService.GetString("AddOns_ItemUnknown");

        GetChromaKeyAddOnCommand = new(GetChromaKeyAddOn, () => IsChromaKeyAddOnAvailable);
        GoBackCommand = new(GoBack, () => _appNavigation.CanGoBack);
    }

    public override Task LoadAsync(CancellationToken cancellationToken)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Load, async () =>
        {
            var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
            try
            {
                StoreAddOn? addOn = await _storeService.GetAddonProductInfoAsync(AddOns.ChromaKeyBackgroundRemoval);
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

    public override void Dispose()
    {
        _isChromaKeyAddOnAvailable = false;
        _isChromaKeyAddOnOwned = false;
        _chromaKeyAddOnPrice = string.Empty;
        _chromaKeyAddOnLogoImage = null;
        base.Dispose();
    }

    private async void GetChromaKeyAddOn()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.GetChromaKeyAddOn, async () =>
        {
            if (!IsChromaKeyAddOnOwned)
            {
                var hwnd = _appController.GetMainWindowHandle();
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
            _appNavigation.GoBackOrGoHome();
        });
    }
}
