using CaptureTool.Common.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAboutPageViewModel
{
    event EventHandler<(string title, string content)>? ShowDialogRequested;
    
    RelayCommand ShowThirdPartyCommand { get; }
    RelayCommand ShowPrivacyPolicyCommand { get; }
    RelayCommand ShowTermsOfUseCommand { get; }
    RelayCommand ShowDisclaimerOfLiabilityCommand { get; }
    RelayCommand GoBackCommand { get; }
}
