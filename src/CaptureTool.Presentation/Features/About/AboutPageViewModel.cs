using CaptureTool.Application.Abstractions.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.About;

public sealed partial class AboutPageViewModel : ViewModelBase
{
    public AboutPageViewModel(
        ILeaveAboutPageUseCase goBackCommand,
        ILocalizationService localizationService,
        ITelemetryService? telemetryService = null)
    {
        _localizationService = localizationService;

        GoBackCommand = goBackCommand.ToRelayCommand(
            () => new LeaveAboutPageRequest(),
            telemetryService,
            "about.go_back");
        ShowThirdPartyCommand = TelemetryCommandFactory.Relay(
            "about.show_third_party",
            () => ShowDialog("About_ThirdParty_DialogTitle", "About_ThirdParty_DialogContent"),
            telemetryService,
            "about");
        ShowPrivacyPolicyCommand = TelemetryCommandFactory.Relay(
            "about.show_privacy_policy",
            () => ShowDialog("About_PrivacyPolicy_DialogTitle", "About_PrivacyPolicy_DialogContent"),
            telemetryService,
            "about");
        ShowTermsOfUseCommand = TelemetryCommandFactory.Relay(
            "about.show_terms_of_use",
            () => ShowDialog("About_TermsOfUse_DialogTitle", "About_TermsOfUse_DialogContent"),
            telemetryService,
            "about");
        ShowDisclaimerOfLiabilityCommand = TelemetryCommandFactory.Relay(
            "about.show_disclaimer_of_liability",
            () => ShowDialog("About_DisclaimerOfLiability_DialogTitle", "About_DisclaimerOfLiability_DialogContent"),
            telemetryService,
            "about");
    }

    private readonly ILocalizationService _localizationService;
    public event EventHandler<(string title, string content)>? ShowDialogRequested;

    public IRelayCommand ShowThirdPartyCommand { get; }
    public IRelayCommand ShowPrivacyPolicyCommand { get; }
    public IRelayCommand ShowTermsOfUseCommand { get; }
    public IRelayCommand ShowDisclaimerOfLiabilityCommand { get; }
    public IRelayCommand GoBackCommand { get; }

    private void ShowDialog(string titleResourceKey, string contentResourceKey)
    {
        string title = _localizationService.GetString(titleResourceKey);
        string content = _localizationService.GetString(contentResourceKey);
        ShowDialogRequested?.Invoke(this, (title, content));
    }
}
