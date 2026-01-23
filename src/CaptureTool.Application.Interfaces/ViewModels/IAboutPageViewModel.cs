using CaptureTool.Common;
using CaptureTool.Infrastructure.Interfaces.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAboutPageViewModel : IViewModel
{
    event EventHandler<(string title, string content)>? ShowDialogRequested;
    
    IAppCommand ShowThirdPartyCommand { get; }
    IAppCommand ShowPrivacyPolicyCommand { get; }
    IAppCommand ShowTermsOfUseCommand { get; }
    IAppCommand ShowDisclaimerOfLiabilityCommand { get; }
    IAppCommand GoBackCommand { get; }
}
