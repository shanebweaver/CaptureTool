using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Store;
using System;
using System.Threading;
using System.Threading.Tasks;
using static CaptureTool.Core.CaptureToolStoreProducts;

namespace CaptureTool.ViewModels;

public sealed partial class AddOnsPageViewModel : AsyncLoadableViewModelBase
{
    private readonly IAppController _appController;
    private readonly IStoreService _storeService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _localizationService;

    public RelayCommand GetChromaKeyAddOnCommand => new(GetChromaKeyAddOn, () => IsChromaKeyAddOnAvailable);
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
        IAppController appController,
        ILocalizationService localizationService,
        IStoreService storeService,
        INavigationService navigationService)
    {
        _appController = appController;
        _storeService = storeService;
        _navigationService = navigationService;
        _localizationService = localizationService;
        _chromaKeyAddOnPrice = localizationService.GetString("AddOns_ItemUnknown");
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
            ChromaKeyAddOnLogoImage = addOn.LogoImage;
        }
        else
        {
            IsChromaKeyAddOnAvailable = false;
            IsChromaKeyAddOnOwned = false;
            ChromaKeyAddOnPrice = _localizationService.GetString("AddOns_ItemNotAvailable");
            ChromaKeyAddOnLogoImage = null;
        }

        await base.LoadAsync(parameter, cancellationToken);
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
    }

    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
