using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.Store.PurchaseChromaKeyAddOn;
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
        ICancellationService cancellationService)
    {
        _localizationService = localizationService;
        _cancellationService = cancellationService;

        ChromaKeyAddOnPrice = localizationService.GetString("AddOns_ItemUnknown");
        GoBackCommand = leaveStorePageCommand.ToRelayCommand(() => new LeaveStorePageRequest());
        PurchaseChromaKeyAddOnCommand = new AsyncRelayCommand(
            PurchaseChromaKeyAddOnAsync,
            () => CanPurchaseChromaKeyAddOn,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        _purchaseChromaKeyAddOnCommand = purchaseChromaKeyAddOnCommand;
        _getChromaKeyAddOnQuery = getChromaKeyAddOnQuery;
    }

    private readonly ILocalizationService _localizationService;
    private readonly ICancellationService _cancellationService;
    private readonly IPurchaseChromaKeyAddOnUseCase _purchaseChromaKeyAddOnCommand;
    private readonly IGetChromaKeyAddOnUseCase _getChromaKeyAddOnQuery;

    public IAsyncRelayCommand PurchaseChromaKeyAddOnCommand { get; }
    public IRelayCommand GoBackCommand { get; }

    public bool IsChromaKeyAddOnOwned
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(CanPurchaseChromaKeyAddOn));
                PurchaseChromaKeyAddOnCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ChromaKeyAddOnPrice
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(ChromaKeyAddOnPurchaseButtonText));
            }
        }
    }

    public Uri? ChromaKeyAddOnLogoImage
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsChromaKeyAddOnAvailable
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(CanPurchaseChromaKeyAddOn));
                PurchaseChromaKeyAddOnCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsPurchasingChromaKeyAddOn
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(CanPurchaseChromaKeyAddOn));
                RaisePropertyChanged(nameof(ChromaKeyAddOnPurchaseButtonText));
                PurchaseChromaKeyAddOnCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasChromaKeyAddOnPurchaseFailure
    {
        get;
        private set => Set(ref field, value);
    }

    public bool CanPurchaseChromaKeyAddOn =>
        IsChromaKeyAddOnAvailable &&
        !IsChromaKeyAddOnOwned &&
        !IsPurchasingChromaKeyAddOn;

    public string ChromaKeyAddOnPurchaseButtonText =>
        IsPurchasingChromaKeyAddOn ? string.Empty : ChromaKeyAddOnPrice;

    public override async Task LoadAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        using CancellationTokenSource cts =
            _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        await RefreshChromaKeyAddOnAsync(false, cts.Token);
        await base.LoadAsync(cts.Token);
    }

    private async Task PurchaseChromaKeyAddOnAsync(CancellationToken cancellationToken)
    {
        if (!CanPurchaseChromaKeyAddOn)
        {
            return;
        }

        HasChromaKeyAddOnPurchaseFailure = false;
        IsPurchasingChromaKeyAddOn = true;
        using CancellationTokenSource cts =
            _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            var response = await _purchaseChromaKeyAddOnCommand.ExecuteAsync(
                new PurchaseChromaKeyAddOnRequest(),
                cts.Token);
            if (response.Value?.Purchased != true)
            {
                HasChromaKeyAddOnPurchaseFailure = true;
                return;
            }

            await RefreshChromaKeyAddOnAsync(true, cts.Token);
        }
        catch (OperationCanceledException)
        {
            HasChromaKeyAddOnPurchaseFailure = true;
        }
        finally
        {
            IsPurchasingChromaKeyAddOn = false;
        }
    }

    private async Task RefreshChromaKeyAddOnAsync(
        bool purchaseConfirmed,
        CancellationToken cancellationToken)
    {
        var response = await _getChromaKeyAddOnQuery.ExecuteAsync(
            new GetChromaKeyAddOnRequest(),
            cancellationToken);
        ApplyChromaKeyAddOn(response.Value?.AddOn, purchaseConfirmed);
    }

    private void ApplyChromaKeyAddOn(IStoreAddOn? addOn, bool purchaseConfirmed)
    {
        if (addOn is not null)
        {
            bool isOwned = purchaseConfirmed || addOn.IsOwned;
            IsChromaKeyAddOnAvailable = !isOwned;
            IsChromaKeyAddOnOwned = isOwned;
            ChromaKeyAddOnPrice = isOwned
                ? _localizationService.GetString("AddOns_ItemOwned")
                : addOn.Price;
            ChromaKeyAddOnLogoImage = addOn.LogoImage;
        }
        else if (purchaseConfirmed)
        {
            IsChromaKeyAddOnAvailable = false;
            IsChromaKeyAddOnOwned = true;
            ChromaKeyAddOnPrice = _localizationService.GetString("AddOns_ItemOwned");
        }
        else
        {
            IsChromaKeyAddOnAvailable = false;
            IsChromaKeyAddOnOwned = false;
            ChromaKeyAddOnPrice = _localizationService.GetString("AddOns_ItemNotAvailable");
            ChromaKeyAddOnLogoImage = null;
        }

        if (IsChromaKeyAddOnOwned)
        {
            HasChromaKeyAddOnPurchaseFailure = false;
        }
    }
}
