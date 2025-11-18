using CaptureTool.Common.Commands;
using CaptureTool.Core.Navigation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Telemetry;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class AboutPageViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string ShowThirdParty = "AboutPageViewModel_ShowThirdParty";
        public static readonly string ShowPrivacyPolicy = "AboutPageViewModel_ShowPrivacyPolicy";
        public static readonly string ShowDisclaimerOfLiability = "AboutPageViewModel_ShowDisclaimerOfLiability";
        public static readonly string ShowTermsOfUse = "AboutPageViewModel_ShowTermsOfUse";
    }

    private readonly AppNavigation _appNavigation;
    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;

    public event EventHandler<(string title, string content)>? ShowDialogRequested;

    public RelayCommand ShowThirdPartyCommand =>
        new(() => ShowDialog("About_ThirdParty_DialogTitle", "About_ThirdParty_DialogContent", ActivityIds.ShowThirdParty));
    public RelayCommand ShowPrivacyPolicyCommand => 
        new(() => ShowDialog("About_PrivacyPolicy_DialogTitle", "About_PrivacyPolicy_DialogContent", ActivityIds.ShowPrivacyPolicy));
    public RelayCommand ShowTermsOfUseCommand => 
        new(() => ShowDialog("About_TermsOfUse_DialogTitle", "About_TermsOfUse_DialogContent", ActivityIds.ShowTermsOfUse));
    public RelayCommand ShowDisclaimerOfLiabilityCommand => 
        new(() => ShowDialog("About_DisclaimerOfLiability_DialogTitle", "About_DisclaimerOfLiability_DialogContent", ActivityIds.ShowDisclaimerOfLiability));
    public RelayCommand GoBackCommand => new(GoBack);

    public AboutPageViewModel(
        AppNavigation appNavigation,
        ILocalizationService localizationService,
        ITelemetryService telemetryService)
    {
        _appNavigation = appNavigation;
        _localizationService = localizationService;
        _telemetryService = telemetryService;
    }

    private void ShowDialog(string titleResourceKey, string contentResourceKey, string activityId)
    {
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            string title = _localizationService.GetString(titleResourceKey);
            string content = _localizationService.GetString(contentResourceKey);
            ShowDialogRequested?.Invoke(this, (title, content));
        }
        catch (Exception ex)
        {
            _telemetryService.ActivityError(activityId, ex);
        }

        _telemetryService.ActivityInitiated(activityId);
    }

    private void GoBack()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
