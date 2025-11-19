using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core;
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
        public static readonly string GoBack = "AboutPageViewModel_GoBack";
    }

    private readonly IAppNavigation _appNavigation;
    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;

    public event EventHandler<(string title, string content)>? ShowDialogRequested;

    public RelayCommand ShowThirdPartyCommand { get; }
    public RelayCommand ShowPrivacyPolicyCommand { get; }
    public RelayCommand ShowTermsOfUseCommand { get; }
    public RelayCommand ShowDisclaimerOfLiabilityCommand { get; }
    public RelayCommand GoBackCommand { get; }

    public AboutPageViewModel(
        IAppNavigation appNavigation,
        ILocalizationService localizationService,
        ITelemetryService telemetryService)
    {
        _appNavigation = appNavigation;
        _localizationService = localizationService;
        _telemetryService = telemetryService;

        ShowThirdPartyCommand = new(() => ShowDialog("About_ThirdParty_DialogTitle", "About_ThirdParty_DialogContent", ActivityIds.ShowThirdParty));
        ShowPrivacyPolicyCommand = new(() => ShowDialog("About_PrivacyPolicy_DialogTitle", "About_PrivacyPolicy_DialogContent", ActivityIds.ShowPrivacyPolicy));
        ShowTermsOfUseCommand = new(() => ShowDialog("About_TermsOfUse_DialogTitle", "About_TermsOfUse_DialogContent", ActivityIds.ShowTermsOfUse));
        ShowDisclaimerOfLiabilityCommand = new(() => ShowDialog("About_DisclaimerOfLiability_DialogTitle", "About_DisclaimerOfLiability_DialogContent", ActivityIds.ShowDisclaimerOfLiability));
        GoBackCommand = new(GoBack);
    }

    private void ShowDialog(string titleResourceKey, string contentResourceKey, string activityId)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, activityId, () =>
        {
            string title = _localizationService.GetString(titleResourceKey);
            string content = _localizationService.GetString(contentResourceKey);
            ShowDialogRequested?.Invoke(this, (title, content));
        });
    }

    private void GoBack()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.GoBack, () =>
        {
            _appNavigation.GoBackOrGoHome();
        });
    }
}
