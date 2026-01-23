using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAboutPageViewModel
{
    event EventHandler<(string title, string content)>? ShowDialogRequested;
    
    ICommand ShowThirdPartyCommand { get; }
    ICommand ShowPrivacyPolicyCommand { get; }
    ICommand ShowTermsOfUseCommand { get; }
    ICommand ShowDisclaimerOfLiabilityCommand { get; }
    ICommand GoBackCommand { get; }
}
