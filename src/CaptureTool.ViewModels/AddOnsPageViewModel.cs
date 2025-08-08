using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Store;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class AddOnsPageViewModel : LoadableViewModelBase
{
    private readonly IAppController _appController;
    private readonly IStoreService _storeService;
    private readonly INavigationService _navigationService;

    public RelayCommand GetChromaKeyAddOnCommand => new(GetChromaKeyAddOn, () => IsChromaKeyFeatureEnabled);
    public RelayCommand GoBackCommand => new(GoBack, () => _navigationService.CanGoBack);

    private bool _isChromaKeyAddOnOwned;
    public bool IsChromaKeyAddOnOwned
    {
        get => _isChromaKeyAddOnOwned;
        set => Set(ref  _isChromaKeyAddOnOwned, value);
    }

    private string _chromaKeyAddOnPrice = string.Empty;
    public string ChromaKeyAddOnPrice
    {
        get => _chromaKeyAddOnPrice;
        set => Set(ref _chromaKeyAddOnPrice, value);
    }

    public bool IsChromaKeyFeatureEnabled { get; }

    public AddOnsPageViewModel(
        IAppController appController,
        IStoreService storeService,
        INavigationService navigationService,
        IFeatureManager featureManager)
    {
        _appController = appController;
        _storeService = storeService;
        _navigationService = navigationService;

        IsChromaKeyFeatureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey);
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        StoreAddOn? addOn = await _storeService.GetAddonProductInfoAsync(CaptureToolStoreProductIds.AddOns.ChromaKeyBackgroundRemoval);
        if (addOn != null)
        {
            IsChromaKeyAddOnOwned = addOn.IsOwned;
            ChromaKeyAddOnPrice = addOn.Price;
        }
        
        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        _isChromaKeyAddOnOwned = false;
        _chromaKeyAddOnPrice = string.Empty;
        base.Unload();
    }

    private async void GetChromaKeyAddOn()
    {
        if (IsChromaKeyFeatureEnabled && !IsChromaKeyAddOnOwned)
        {
            var hwnd = _appController.GetMainWindowHandle();
            bool success = await _storeService.PurchaseAddonAsync(CaptureToolStoreProductIds.AddOns.ChromaKeyBackgroundRemoval, hwnd);
            IsChromaKeyAddOnOwned = success;
        }
    }

    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
