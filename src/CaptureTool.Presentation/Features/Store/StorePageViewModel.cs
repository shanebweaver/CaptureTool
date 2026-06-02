using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Features.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Features.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.Features.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Store;

public sealed partial class StorePageViewModel : AsyncLoadableViewModelBase
{
    public StorePageViewModel(
        ILeaveStorePageUseCase leaveStorePageCommand,
        IPurchaseChromaKeyAddOnUseCase purchaseChromaKeyAddOnCommand,
        IGetChromaKeyAddOnUseCase getChromaKeyAddOnQuery,
        ILocalizationService localizationService,
        ICancellationService cancellationService,
        ITelemetryService telemetryService)
    {
        _localizationService = localizationService;
        _cancellationService = cancellationService;
        _telemetryService = telemetryService;

        ChromaKeyAddOnPrice = localizationService.GetString("AddOns_ItemUnknown");
        GoBackCommand = leaveStorePageCommand.ToRelayCommand(() => new LeaveStorePageRequest(), telemetryService);
        PurchaseChromaKeyAddOnCommand = purchaseChromaKeyAddOnCommand.ToAsyncRelayCommand(() => new PurchaseChromaKeyAddOnRequest(), telemetryService);
        _getChromaKeyAddOnQuery = getChromaKeyAddOnQuery;
    }

    private readonly ILocalizationService _localizationService;
    private readonly ICancellationService _cancellationService;
    private readonly IGetChromaKeyAddOnUseCase _getChromaKeyAddOnQuery;
    private readonly ITelemetryService _telemetryService;

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
            IStoreAddOn? addOn = null;
            try
            {
                addOn = (await _getChromaKeyAddOnQuery.ExecuteAsync(new GetChromaKeyAddOnRequest(), cancellationToken)).AddOn;
            }
            catch (OperationCanceledException exception)
            {
                _telemetryService.ActivityCanceled(nameof(LoadAsync), exception.Message);
            }
            catch (Exception exception)
            {
                _telemetryService.ActivityError(nameof(LoadAsync), exception);
            }

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
