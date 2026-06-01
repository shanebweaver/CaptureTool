using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.About.LeaveAboutPage;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.ViewModels;
using CaptureTool.Presentation.Shared.Commands;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.About;

public sealed partial class AboutPageViewModel : ViewModelBase
{
    public AboutPageViewModel(
        LeaveAboutPageUseCase goBackCommand,
        ILocalizationService localizationService,
        ITelemetryService telemetryService)
    {
        _localizationService = localizationService;
        _telemetryService = telemetryService;

        GoBackCommand = goBackCommand.ToRelayCommand(() => new LeaveAboutPageRequest(), telemetryService);
        ShowThirdPartyCommand = new RelayCommand(() => ShowDialog("About_ThirdParty_DialogTitle", "About_ThirdParty_DialogContent"));
        ShowPrivacyPolicyCommand = new RelayCommand(() => ShowDialog("About_PrivacyPolicy_DialogTitle", "About_PrivacyPolicy_DialogContent"));
        ShowTermsOfUseCommand = new RelayCommand(() => ShowDialog("About_TermsOfUse_DialogTitle", "About_TermsOfUse_DialogContent"));
        ShowDisclaimerOfLiabilityCommand = new RelayCommand(() => ShowDialog("About_DisclaimerOfLiability_DialogTitle", "About_DisclaimerOfLiability_DialogContent"));
    }

    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;
    public event EventHandler<(string title, string content)>? ShowDialogRequested;

    public IRelayCommand ShowThirdPartyCommand { get; }
    public IRelayCommand ShowPrivacyPolicyCommand { get; }
    public IRelayCommand ShowTermsOfUseCommand { get; }
    public IRelayCommand ShowDisclaimerOfLiabilityCommand { get; }
    public IRelayCommand GoBackCommand { get; }

    private void ShowDialog(string titleResourceKey, string contentResourceKey)
    {
        try
        {
            string title = _localizationService.GetString(titleResourceKey);
            string content = _localizationService.GetString(contentResourceKey);
            ShowDialogRequested?.Invoke(this, (title, content));
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(ShowDialog), exception);
        }
    }
}
