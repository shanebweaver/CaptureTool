using CaptureTool.Core.Settings;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Settings;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace CaptureTool.Services.Windows.Localization;

public sealed partial class WindowsLocalizationService : ILocalizationService
{
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    private readonly ResourceLoader? _resourceLoader;

    public AppLanguage? LanguageOverride { get; private set; }
    public AppLanguage RequestedLanguage { get; private set; }
    public AppLanguage StartupLanguage { get; private set; }
    public AppLanguage DefaultLanguage { get; }
    public AppLanguage[] SupportedLanguages { get; }

    public WindowsLocalizationService(
        ICancellationService cancellationService,
        ISettingsService settingsService)
    {
        _cancellationService = cancellationService;
        _settingsService = settingsService;

        SupportedLanguages = [.. ApplicationLanguages.Languages.Select(l => new AppLanguage(l))];
        DefaultLanguage = new(CultureInfo.InstalledUICulture.Name);
    
        string languageOverride = _settingsService.Get(CaptureToolSettings.Settings_LanguageOverride);
        ApplicationLanguages.PrimaryLanguageOverride = languageOverride;
        LanguageOverride = string.IsNullOrEmpty(languageOverride) ? null : new(languageOverride);

        StartupLanguage = LanguageOverride ?? DefaultLanguage;
        RequestedLanguage = StartupLanguage;

        _resourceLoader = ResourceLoader.GetForViewIndependentUse();
    }

    public string GetString(string resourceKey)
    {
        Debug.Assert(_resourceLoader != null);
        return _resourceLoader.GetString(resourceKey);
    }

    public void OverrideLanguage(AppLanguage? language)
    {
        if (language == null)
        {
            LanguageOverride = null;
            RequestedLanguage = DefaultLanguage;
            _settingsService.Unset(CaptureToolSettings.Settings_LanguageOverride);
            ApplicationLanguages.PrimaryLanguageOverride = null;
        }
        else
        {
            bool isLanguageSupported = SupportedLanguages.Any(i => i.Value == language.Value.Value);
            if (!isLanguageSupported)
            {
                throw new ArgumentOutOfRangeException(nameof(language));
            }

            LanguageOverride = language.Value;
            RequestedLanguage = language.Value;
            _settingsService.Set(CaptureToolSettings.Settings_LanguageOverride, language.Value.Value);
            ApplicationLanguages.PrimaryLanguageOverride = language.Value.Value;
        }

        var token = _cancellationService.GetLinkedCancellationTokenSource().Token;
        _ = _settingsService.TrySaveAsync(token);
    }
}
