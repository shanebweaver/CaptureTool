using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Telemetry;

internal sealed class TelemetryConsentDialogService
{
    private readonly ISettingsService _settingsService;
    private readonly ITelemetryConsentService _consentService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private ResourceLoader? _resourceLoader;

    public TelemetryConsentDialogService(
        ISettingsService settingsService,
        ITelemetryConsentService consentService)
    {
        _settingsService = settingsService;
        _consentService = consentService;
    }

    public XamlRoot? XamlRoot { get; set; }

    public async Task RequestConsentIfNeededAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (XamlRoot is null || _consentService.State != TelemetryConsentState.Unknown)
            {
                return;
            }

            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = GetString("TelemetryConsentDialog_Title", "Help improve Capture Tool"),
                Content = new TextBlock
                {
                    Text = GetString(
                        "TelemetryConsentDialog_Content",
                        "Share optional, aggregate usage data, such as how often features and buttons are used. " +
                        "Capture Tool submits only short event names to Microsoft Store Services. It does not include your " +
                        "captures, file names, paths, clipboard contents, recognized text, or app-generated user, device, " +
                        "installation, or session identifiers. You can change this anytime in Settings."),
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                PrimaryButtonText = GetString("TelemetryConsentDialog_AllowButton", "Share usage data"),
                SecondaryButtonText = GetString("TelemetryConsentDialog_DontAllowButton", "No thanks"),
                DefaultButton = ContentDialogButton.None
            };
            AutomationProperties.SetAutomationId(dialog, "TelemetryConsentDialog");

            ContentDialogResult result = await dialog.ShowAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            TelemetryConsentState state = result == ContentDialogResult.Primary
                ? TelemetryConsentState.Granted
                : TelemetryConsentState.Denied;
            _settingsService.Set(
                CaptureToolSettings.Settings_TelemetryConsent,
                TelemetryConsentSettingValues.Serialize(state));
            await _settingsService.TrySaveAsync(cancellationToken);
            _consentService.SetState(state);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private string GetString(string resourceKey, string fallback)
    {
        return WinUIResourceLoader.GetString(ref _resourceLoader, resourceKey, fallback);
    }
}
