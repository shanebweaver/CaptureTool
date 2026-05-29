using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Features.Store.LeaveStorePage;
using CaptureTool.Application.Features.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Infrastructure.Abstractions.Cancellation;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Store;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Store;

public sealed partial class StorePageViewModel : AsyncLoadableViewModelBase
{
    public StorePageViewModel(
        IUseCase<LeaveStorePageRequest, LeaveStorePageResponse> leaveStorePageCommand,
        IUseCase<PurchaseChromaKeyAddOnRequest, PurchaseChromaKeyAddOnResponse> purchaseChromaKeyAddOnCommand,
        IUseCase<GetChromaKeyAddOnRequest, GetChromaKeyAddOnResponse> getChromaKeyAddOnQuery,
        ILocalizationService localizationService,
        ICancellationService cancellationService)
    {
        _localizationService = localizationService;
        _cancellationService = cancellationService;

        ChromaKeyAddOnPrice = localizationService.GetString("AddOns_ItemUnknown");
        GoBackCommand = leaveStorePageCommand.ToRelayCommand(() => new LeaveStorePageRequest());
        PurchaseChromaKeyAddOnCommand = purchaseChromaKeyAddOnCommand.ToAsyncRelayCommand(() => new PurchaseChromaKeyAddOnRequest());
        _getChromaKeyAddOnQuery = getChromaKeyAddOnQuery;
    }

    private readonly ILocalizationService _localizationService;
    private readonly ICancellationService _cancellationService;
    private readonly IUseCase<GetChromaKeyAddOnRequest, GetChromaKeyAddOnResponse> _getChromaKeyAddOnQuery;

    public IAsyncRelayCommand PurchaseChromaKeyAddOnCommand { get; }
    public IRelayCommand GoBackCommand { get; }

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
            IStoreAddOn? addOn = (await _getChromaKeyAddOnQuery.ExecuteAsync(new GetChromaKeyAddOnRequest(), cancellationToken)).AddOn;
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
