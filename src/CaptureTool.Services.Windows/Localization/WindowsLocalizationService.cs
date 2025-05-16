using CaptureTool.Services.Localization;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace CaptureTool.Services.Windows.Localization;

public sealed partial class WindowsLocalizationService : ILocalizationService
{
    private ResourceLoader _resourceLoader;
    private CultureInfo _culture;

    public AppLanguage CurrentLanguage { get; }
    public AppLanguage StartupLanguage { get; }
    public AppLanguage[] SupportedLanguages { get; }

    public WindowsLocalizationService()
    {
        InitializeResourceLoader();
        Debug.Assert(_resourceLoader != null);
        Debug.Assert(_culture != null);

        StartupLanguage = CurrentLanguage = new(_culture.Name);
        SupportedLanguages = [.. ApplicationLanguages.Languages.Select(l => new AppLanguage(l))];
    }

    public string GetString(string resourceKey)
    {
        return _resourceLoader.GetString(resourceKey);
    }

    public void UpdateCurrentLanguage(AppLanguage language)
    {
        ApplicationLanguages.PrimaryLanguageOverride = language.Value;
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
