using CaptureTool.Presentation.ViewModels.Helpers;
using CaptureTool.Application.Abstractions.UseCases.About;
using CaptureTool.Infrastructure.UseCases.Extensions;
using CaptureTool.Infrastructure.ViewModels;
using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Telemetry;

namespace CaptureTool.Presentation.ViewModels;

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
