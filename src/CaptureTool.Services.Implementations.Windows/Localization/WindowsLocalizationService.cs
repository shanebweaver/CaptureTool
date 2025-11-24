using CaptureTool.Services.Interfaces.Localization;
using System.Diagnostics;
using System.Globalization;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace CaptureTool.Services.Implementations.Windows.Localization;

public sealed partial class WindowsLocalizationService : ILocalizationService
{
    private ResourceLoader? _resourceLoader;
    private bool _isInitialized;

    public IAppLanguage? LanguageOverride { get; private set; }
    public IAppLanguage? RequestedLanguage { get; private set; }
    public IAppLanguage? StartupLanguage { get; private set; }
    public IAppLanguage DefaultLanguage { get; }
    public IAppLanguage[] SupportedLanguages { get; }

    public WindowsLocalizationService()
    {
        SupportedLanguages = [.. ApplicationLanguages.Languages.Select(l => new WindowsAppLanguage(l))];
        DefaultLanguage = new WindowsAppLanguage(CultureInfo.InstalledUICulture.Name);
    }

    public void Initialize(string languageOverride)
    {
        if (_isInitialized)
        {
            return;
        }

        ApplicationLanguages.PrimaryLanguageOverride = languageOverride;
        LanguageOverride = string.IsNullOrEmpty(languageOverride) ? null : new WindowsAppLanguage(languageOverride);

        StartupLanguage = LanguageOverride ?? DefaultLanguage;
        RequestedLanguage = StartupLanguage;

        _resourceLoader = ResourceLoader.GetForViewIndependentUse();

        _isInitialized = true;
    }

    public string GetString(string resourceKey)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("The localization service has not been initialized.");
        }

        Debug.Assert(_resourceLoader != null);
        return _resourceLoader.GetString(resourceKey);
    }

    public void OverrideLanguage(IAppLanguage? language)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("The localization service has not been initialized.");
        }

        if (language == null)
        {
            LanguageOverride = null;
            RequestedLanguage = DefaultLanguage;
            ApplicationLanguages.PrimaryLanguageOverride = null;
        }
        else
        {
            bool isLanguageSupported = SupportedLanguages.Any(i => i.Value == language.Value);
            if (!isLanguageSupported)
            {
                throw new ArgumentOutOfRangeException(nameof(language));
            }

            LanguageOverride = new WindowsAppLanguage(language.Value);
            RequestedLanguage = new WindowsAppLanguage(language.Value);
            ApplicationLanguages.PrimaryLanguageOverride = language.Value;
        }
    }
}
