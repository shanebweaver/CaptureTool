using CaptureTool.Application.Abstractions.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Feedback;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.About;

public sealed partial class AboutPageViewModel : ViewModelBase
{
    public AboutPageViewModel(
        ILeaveAboutPageUseCase goBackCommand,
        IFeedbackHubService feedbackHubService,
        ILocalizationService localizationService)
    {
        _feedbackHubService = feedbackHubService;
        _localizationService = localizationService;

        GoBackCommand = goBackCommand.ToRelayCommand(() => new LeaveAboutPageRequest());
        ShowThirdPartyCommand = new RelayCommand(() => ShowDialog("About_ThirdParty_DialogTitle", "About_ThirdParty_DialogContent"));
        ShowPrivacyPolicyCommand = new RelayCommand(() => ShowDialog("About_PrivacyPolicy_DialogTitle", "About_PrivacyPolicy_DialogContent"));
        ShowTermsOfUseCommand = new RelayCommand(() => ShowDialog("About_TermsOfUse_DialogTitle", "About_TermsOfUse_DialogContent"));
        ShowDisclaimerOfLiabilityCommand = new RelayCommand(() => ShowDialog("About_DisclaimerOfLiability_DialogTitle", "About_DisclaimerOfLiability_DialogContent"));
        SendFeedbackCommand = new AsyncRelayCommand(SendFeedbackAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    private readonly IFeedbackHubService _feedbackHubService;
    private readonly ILocalizationService _localizationService;
    public event EventHandler<(string title, string content)>? ShowDialogRequested;

    public IRelayCommand ShowThirdPartyCommand { get; }
    public IRelayCommand ShowPrivacyPolicyCommand { get; }
    public IRelayCommand ShowTermsOfUseCommand { get; }
    public IRelayCommand ShowDisclaimerOfLiabilityCommand { get; }
    public IAsyncRelayCommand SendFeedbackCommand { get; }
    public IRelayCommand GoBackCommand { get; }

    private void ShowDialog(string titleResourceKey, string contentResourceKey)
    {
        string title = _localizationService.GetString(titleResourceKey);
        string content = _localizationService.GetString(contentResourceKey);
        ShowDialogRequested?.Invoke(this, (title, content));
    }

    private async Task SendFeedbackAsync()
    {
        await _feedbackHubService.LaunchAsync(CancellationToken.None);
    }
}
