using CaptureTool.Presentation.ViewModels.Helpers;
using CaptureTool.Application.Abstractions.UseCases.AddOns;
using CaptureTool.Infrastructure.UseCases.Extensions;
using CaptureTool.Infrastructure.ViewModels;
using CaptureTool.Infrastructure.Abstractions.Cancellation;
using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Abstractions.Windowing;
using static CaptureTool.Application.Abstractions.Store.CaptureToolStoreProducts;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class AddOnsPageViewModel : AsyncLoadableViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadAddOnsPage";
        public static readonly string GetChromaKeyAddOn = "GetChromaKeyAddOn";
        public static readonly string GoBack = "GoBack";
    }

    private const string TelemetryContext = "AddOnsPage";

    private readonly IAddOnsGoBackUseCase _goBackAction;
    private readonly IWindowHandleProvider _windowingService;
    private readonly IStoreService _storeService;
    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;

    public IAsyncAppCommand GetChromaKeyAddOnCommand { get; }
    public IAppCommand GoBackCommand { get; }

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
        IAddOnsGoBackUseCase goBackAction,
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

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
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
