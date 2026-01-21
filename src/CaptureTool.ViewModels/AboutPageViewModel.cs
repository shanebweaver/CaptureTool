using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Core.Interfaces.Actions.About;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.ViewModels.Helpers;

namespace CaptureTool.ViewModels;

public sealed partial class AboutPageViewModel : ViewModelBase
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

    private readonly IAboutGoBackAction _goBackAction;
    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;

    public event EventHandler<(string title, string content)>? ShowDialogRequested;

    public RelayCommand ShowThirdPartyCommand { get; }
    public RelayCommand ShowPrivacyPolicyCommand { get; }
    public RelayCommand ShowTermsOfUseCommand { get; }
    public RelayCommand ShowDisclaimerOfLiabilityCommand { get; }
    public RelayCommand GoBackCommand { get; }

    public AboutPageViewModel(
        IAboutGoBackAction goBackAction,
        ILocalizationService localizationService,
        ITelemetryService telemetryService)
    {
        _goBackAction = goBackAction;
        _localizationService = localizationService;
        _telemetryService = telemetryService;

        TelemetryCommandFactory commandFactory = new(telemetryService, TelemetryContext);
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
