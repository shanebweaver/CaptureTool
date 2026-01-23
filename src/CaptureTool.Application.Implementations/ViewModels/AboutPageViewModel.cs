using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.UseCases.About;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Common;
using CaptureTool.Infrastructure.Implementations.UseCases.Extensions;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class AboutPageViewModel : ViewModelBase, IAboutPageViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string ShowThirdParty = "ShowThirdParty";
        public static readonly string ShowPrivacyPolicy = "ShowPrivacyPolicy";
        public static readonly string ShowDisclaimerOfLiability = "ShowDisclaimerOfLiability";
        public static readonly string ShowTermsOfUse = "ShowTermsOfUse";
        public static readonly string GoBack = "GoBack";
    }

    private const string TelemetryContext = "AboutPage";

    private readonly IAboutGoBackUseCase _goBackAction;
    private readonly ILocalizationService _localizationService;
    public event EventHandler<(string title, string content)>? ShowDialogRequested;

    public IAppCommand ShowThirdPartyCommand { get; }
    public IAppCommand ShowPrivacyPolicyCommand { get; }
    public IAppCommand ShowTermsOfUseCommand { get; }
    public IAppCommand ShowDisclaimerOfLiabilityCommand { get; }
    public IAppCommand GoBackCommand { get; }

    public AboutPageViewModel(
        IAboutGoBackUseCase goBackAction,
        ILocalizationService localizationService,
        ITelemetryService telemetryService)
    {
        _goBackAction = goBackAction;
        _localizationService = localizationService;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        ShowThirdPartyCommand = commandFactory.Create(ActivityIds.ShowThirdParty, () => ShowDialog("About_ThirdParty_DialogTitle", "About_ThirdParty_DialogContent"));
        ShowPrivacyPolicyCommand = commandFactory.Create(ActivityIds.ShowPrivacyPolicy, () => ShowDialog("About_PrivacyPolicy_DialogTitle", "About_PrivacyPolicy_DialogContent"));
        ShowTermsOfUseCommand = commandFactory.Create(ActivityIds.ShowTermsOfUse, () => ShowDialog("About_TermsOfUse_DialogTitle", "About_TermsOfUse_DialogContent"));
        ShowDisclaimerOfLiabilityCommand = commandFactory.Create(ActivityIds.ShowDisclaimerOfLiability, () => ShowDialog("About_DisclaimerOfLiability_DialogTitle", "About_DisclaimerOfLiability_DialogContent"));
        GoBackCommand = commandFactory.Create(ActivityIds.GoBack, GoBack, () => _goBackAction.CanExecute());
    }

    private void ShowDialog(string titleResourceKey, string contentResourceKey)
    {
        string title = _localizationService.GetString(titleResourceKey);
        string content = _localizationService.GetString(contentResourceKey);
        ShowDialogRequested?.Invoke(this, (title, content));
    }

    private void GoBack()
    {
        _goBackAction.ExecuteCommand();
    }
}
