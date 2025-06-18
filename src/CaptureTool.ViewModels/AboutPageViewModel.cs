using System;
using CaptureTool.Common.Commands;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Telemetry;

namespace CaptureTool.ViewModels;

public sealed partial class AboutPageViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string ShowPrivacyPolicy = "AboutPageViewModel_ShowPrivacyPolicy";
        public static readonly string ShowDisclaimerOfLiability = "AboutPageViewModel_ShowDisclaimerOfLiability";
        public static readonly string ShowTermsOfUse = "AboutPageViewModel_ShowTermsOfUse";
    }

    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;

    public event EventHandler<(string title, string content)>? ShowDialogRequested;

    public RelayCommand ShowPrivacyPolicyCommand => new(ShowPrivacyPolicy);
    public RelayCommand ShowTermsOfUseCommand => new(ShowTermsOfUse);
    public RelayCommand ShowDisclaimerOfLiabilityCommand => new(ShowDisclaimerOfLiability);

    public AboutPageViewModel(
        ILocalizationService localizationService,
        ITelemetryService telemetryService)
    {
        _localizationService = localizationService;
        _telemetryService = telemetryService;
    }

    private void ShowPrivacyPolicy()
    {
        string activityId = ActivityIds.ShowPrivacyPolicy;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            string title = _localizationService.GetString("About_PrivacyPolicy_Title");
            string content = _localizationService.GetString("About_PrivacyPolicy_Content");
            ShowDialogRequested?.Invoke(this, (title, content));
        }
        catch (Exception ex)
        {
            _telemetryService.ActivityError(activityId, ex);
        }

        _telemetryService.ActivityInitiated(activityId);
    }

    private void ShowTermsOfUse()
    {
        string activityId = ActivityIds.ShowTermsOfUse;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            string title = _localizationService.GetString("About_TermsOfUse_Title");
            string content = _localizationService.GetString("About_TermsOfUse_Content");
            ShowDialogRequested?.Invoke(this, (title, content));
        }
        catch (Exception ex)
        {
            _telemetryService.ActivityError(activityId, ex);
        }

        _telemetryService.ActivityInitiated(activityId);
    }

    private void ShowDisclaimerOfLiability()
    {
        string activityId = ActivityIds.ShowDisclaimerOfLiability;
        _telemetryService.ActivityInitiated(activityId);

        try 
        {
            string title = _localizationService.GetString("About_DisclaimerOfLiability_Title");
            string content = _localizationService.GetString("About_DisclaimerOfLiability_Content");
            ShowDialogRequested?.Invoke(this, (title, content));
        }
        catch (Exception ex)
        {
            _telemetryService.ActivityError(activityId, ex);
        }

        _telemetryService.ActivityInitiated(activityId);
    }
}
