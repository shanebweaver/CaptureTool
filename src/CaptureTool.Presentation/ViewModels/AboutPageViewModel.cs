using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Commands;
using CaptureTool.Infrastructure.ViewModels;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class AboutPageViewModel : ViewModelBase
{
    public AboutPageViewModel(
        IGoBackAppCommand goBackCommand,
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;

        GoBackCommand = goBackCommand;
        ShowThirdPartyCommand = AppCommand.Create(() => ShowDialog("About_ThirdParty_DialogTitle", "About_ThirdParty_DialogContent"));
        ShowPrivacyPolicyCommand = AppCommand.Create(() => ShowDialog("About_PrivacyPolicy_DialogTitle", "About_PrivacyPolicy_DialogContent"));
        ShowTermsOfUseCommand = AppCommand.Create(() => ShowDialog("About_TermsOfUse_DialogTitle", "About_TermsOfUse_DialogContent"));
        ShowDisclaimerOfLiabilityCommand = AppCommand.Create(() => ShowDialog("About_DisclaimerOfLiability_DialogTitle", "About_DisclaimerOfLiability_DialogContent"));
    }

    private readonly ILocalizationService _localizationService;
    public event EventHandler<(string title, string content)>? ShowDialogRequested;

    public IAppCommand ShowThirdPartyCommand { get; }
    public IAppCommand ShowPrivacyPolicyCommand { get; }
    public IAppCommand ShowTermsOfUseCommand { get; }
    public IAppCommand ShowDisclaimerOfLiabilityCommand { get; }
    public IAppCommand GoBackCommand { get; }

    private void ShowDialog(string titleResourceKey, string contentResourceKey)
    {
        string title = _localizationService.GetString(titleResourceKey);
        string content = _localizationService.GetString(contentResourceKey);
        ShowDialogRequested?.Invoke(this, (title, content));
    }
}
