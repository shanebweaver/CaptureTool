using CaptureTool.Services.Localization;
using System.Diagnostics;
using System.Globalization;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace CaptureTool.Services.Windows.Localization;

public sealed partial class WindowsLocalizationService : ILocalizationService
{
    private ResourceLoader _resourceLoader;
    private CultureInfo _culture;

    public string CurrentLanguage { get; }
    public string StartupLanguage { get; }
    public string[] SupportedLanguages { get; }

    public WindowsLocalizationService()
    {
        InitializeResourceLoader();
        Debug.Assert(_resourceLoader != null);
        Debug.Assert(_culture != null);

        StartupLanguage = CurrentLanguage = _culture.Name;
        SupportedLanguages = [.. ApplicationLanguages.Languages];
    }

    public string GetString(string resourceKey)
    {
        return _resourceLoader.GetString(resourceKey);
    }

    public void UpdateCurrentLanguage(string language)
    {
        ApplicationLanguages.PrimaryLanguageOverride = language;
        InitializeResourceLoader();
    }

    private void InitializeResourceLoader()
    {
        _resourceLoader = new ResourceLoader();

        string languageOverride = ApplicationLanguages.PrimaryLanguageOverride;
        if (!string.IsNullOrEmpty(languageOverride))
        {
            _culture = new CultureInfo(languageOverride);
        }
        else
        {
            _culture = CultureInfo.InstalledUICulture;
        }
    }
}
