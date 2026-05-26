using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Cancellation;
using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Queries;
using CaptureTool.Infrastructure.Abstractions.Store;
using CaptureTool.Infrastructure.ViewModels;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class StorePageViewModel : AsyncLoadableViewModelBase
{
    public StorePageViewModel(
        ILeaveStorePageAppCommand leaveStorePageCommand,
        IPurchaseChromaKeyAddOnAppCommand purchaseChromaKeyAddOnCommand,
        IGetChromaKeyAddOnAppQuery getChromaKeyAddOnQuery,
        ILocalizationService localizationService,
        ICancellationService cancellationService)
    {
        _localizationService = localizationService;
        _cancellationService = cancellationService;

        ChromaKeyAddOnPrice = localizationService.GetString("AddOns_ItemUnknown");
        GoBackCommand = leaveStorePageCommand;
        PurchaseChromaKeyAddOnCommand = purchaseChromaKeyAddOnCommand;
        GetChromaKeyAddOnQuery = getChromaKeyAddOnQuery;
    }

    private readonly ILocalizationService _localizationService;
    private readonly ICancellationService _cancellationService;

    public IAsyncAppCommand PurchaseChromaKeyAddOnCommand { get; }
    public IAsyncAppQuery<IStoreAddOn> GetChromaKeyAddOnQuery { get; }
    public IAppCommand GoBackCommand { get; }

    public bool IsChromaKeyAddOnOwned
    {
        get;
        private set => Set(ref field, value);
    }

    public string ChromaKeyAddOnPrice
    {
        get;
        private set => Set(ref field, value);
    }

    public Uri? ChromaKeyAddOnLogoImage
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsChromaKeyAddOnAvailable
    {
        get;
        private set => Set(ref field, value);
    }

    public override async Task LoadAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            IStoreAddOn? addOn = await GetChromaKeyAddOnQuery.ExecuteAsync(cancellationToken);
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
    }
}
