using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Store;
using System.Threading;
using System.Threading.Tasks;
using static CaptureTool.Core.CaptureToolStoreProducts;

namespace CaptureTool.ViewModels;

public sealed partial class AddOnsPageViewModel : LoadableViewModelBase
{
    private readonly IAppController _appController;
    private readonly IStoreService _storeService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _localizationService;

    public RelayCommand GetChromaKeyAddOnCommand => new(GetChromaKeyAddOn, () => IsChromaKeyFeatureEnabled);
    public RelayCommand GoBackCommand => new(GoBack, () => _navigationService.CanGoBack);

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

    private bool _isChromaKeyAddOnAvailable;
    public bool IsChromaKeyAddOnAvailable
    {
        get => _isChromaKeyAddOnAvailable;
        private set => Set(ref _isChromaKeyAddOnAvailable, value);
    }

    public bool IsChromaKeyFeatureEnabled { get; }

    public AddOnsPageViewModel(
        IAppController appController,
        ILocalizationService localizationService,
        IStoreService storeService,
        INavigationService navigationService,
        IFeatureManager featureManager)
    {
        _appController = appController;
        _storeService = storeService;
        _navigationService = navigationService;
        _localizationService = localizationService;
        _chromaKeyAddOnPrice = localizationService.GetString("AddOns_ItemUnknown");
        IsChromaKeyFeatureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey);
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        StoreAddOn? addOn = await _storeService.GetAddonProductInfoAsync(AddOns.ChromaKeyBackgroundRemoval);
        if (addOn != null)
        {
            bool isOwned = addOn.IsOwned;
            IsChromaKeyAddOnAvailable = !isOwned;
            IsChromaKeyAddOnOwned = isOwned;
            ChromaKeyAddOnPrice = isOwned ? _localizationService.GetString("AddOns_ItemOwned") : addOn.Price;
        }
        else
        {
            IsChromaKeyAddOnAvailable = false;
            IsChromaKeyAddOnOwned = false;
            ChromaKeyAddOnPrice = _localizationService.GetString("AddOns_ItemNotAvailable");
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        _isChromaKeyAddOnAvailable = false;
        _isChromaKeyAddOnOwned = false;
        _chromaKeyAddOnPrice = string.Empty;
        base.Unload();
    }

    private async void GetChromaKeyAddOn()
    {
        if (IsChromaKeyFeatureEnabled && !IsChromaKeyAddOnOwned)
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
    }

    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
