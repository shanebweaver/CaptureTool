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
    private ContentDialog? _activeDialog;
    private bool _isPromptAllowed;
    private int _promptGeneration;
    private ResourceLoader? _resourceLoader;

    public TelemetryConsentDialogService(
        ISettingsService settingsService,
        ITelemetryConsentService consentService)
    {
        _settingsService = settingsService;
        _consentService = consentService;
    }

    public XamlRoot? XamlRoot { get; set; }

    public void AllowPrompt()
    {
        _isPromptAllowed = true;
    }

    public void SuppressPrompt()
    {
        _isPromptAllowed = false;
        _promptGeneration++;

        try
        {
            _activeDialog?.Hide();
        }
        catch (InvalidOperationException)
        {
            // The dialog can finish closing between the null check and Hide.
        }
    }

    public async Task RequestConsentIfNeededAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (!_isPromptAllowed ||
                XamlRoot is null ||
                _consentService.State != TelemetryConsentState.Unknown)
            {
                return;
            }

            int promptGeneration = _promptGeneration;
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = GetString("TelemetryConsentDialog_Title", "Help improve Capture Tool"),
                Content = new TextBlock
                {
                    Text = GetString(
                        "TelemetryConsentDialog_Content",
                        "Allow Capture Tool to send optional usage data through Microsoft Store Services. This data is used " +
                        "to produce aggregate counts of feature and command usage and outcomes for key operations. Capture Tool " +
                        "does not include captures, file names or paths, clipboard contents, recognized text, or app-generated " +
                        "identifiers. You can change this setting at any time in Settings."),
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                PrimaryButtonText = GetString("TelemetryConsentDialog_AllowButton", "Send optional data"),
                SecondaryButtonText = GetString("TelemetryConsentDialog_DontAllowButton", "Don't send"),
                DefaultButton = ContentDialogButton.None
            };
            AutomationProperties.SetAutomationId(dialog, "TelemetryConsentDialog");
            _activeDialog = dialog;

            ContentDialogResult result = await dialog.ShowAsync();
            if (cancellationToken.IsCancellationRequested ||
                !_isPromptAllowed ||
                promptGeneration != _promptGeneration)
            {
                return;
            }

            TelemetryConsentState state = result == ContentDialogResult.Primary
                ? TelemetryConsentState.Granted
                : TelemetryConsentState.Denied;
            SettingsMutationResult saveResult = await _settingsService.TrySetAndSaveAsync(
                CaptureToolSettings.Settings_TelemetryConsent,
                TelemetryConsentSettingValues.Serialize(state),
                cancellationToken);
            if (saveResult.Succeeded)
            {
                _consentService.SetState(state);
            }
        }
        finally
        {
            _activeDialog = null;
            _semaphore.Release();
        }
    }

    private string GetString(string resourceKey, string fallback)
    {
        return WinUIResourceLoader.GetString(ref _resourceLoader, resourceKey, fallback);
    }
}
