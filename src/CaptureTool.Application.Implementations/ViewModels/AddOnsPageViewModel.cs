using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Application.Interfaces.Actions.AddOns;
using CaptureTool.Infrastructure.Interfaces.Cancellation;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Store;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Infrastructure.Interfaces.Windowing;
using CaptureTool.Application.Implementations.ViewModels.Helpers;
using static CaptureTool.Application.Interfaces.Store.CaptureToolStoreProducts;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class AddOnsPageViewModel : AsyncLoadableViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadAddOnsPage";
        public static readonly string GetChromaKeyAddOn = "GetChromaKeyAddOn";
        public static readonly string GoBack = "GoBack";
    }

    private const string TelemetryContext = "AddOnsPage";

    private readonly IAddOnsGoBackAction _goBackAction;
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
        IAddOnsGoBackAction goBackAction,
        IWindowHandleProvider windowingService,
        ILocalizationService localizationService,
        IStoreService storeService,
        ITelemetryService telemetryService,
        ICancellationService cancellationService)
    {
        _goBackAction = goBackAction;
        _windowingService = windowingService;
        _localizationService = localizationService;
        _storeService = storeService;
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;

        ChromaKeyAddOnPrice = localizationService.GetString("AddOns_ItemUnknown");

        TelemetryCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        GetChromaKeyAddOnCommand = commandFactory.CreateAsync(ActivityIds.GetChromaKeyAddOn, GetChromaKeyAddOnAsync, () => IsChromaKeyAddOnAvailable);
        GoBackCommand = commandFactory.Create(ActivityIds.GoBack, GoBack, () => _goBackAction.CanExecute());
    }

    public override Task LoadAsync(CancellationToken cancellationToken)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, TelemetryContext, ActivityIds.Load, async () =>
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

    private async Task GetChromaKeyAddOnAsync()
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
    }

    private void GoBack()
    {
        _goBackAction.ExecuteCommand();
    }
}
