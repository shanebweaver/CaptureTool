using CaptureTool.Application.Abstractions.Localization;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestLocalizationService : ILocalizationService
{
    private readonly IAppLanguage _defaultLanguage = new UiTestAppLanguage("en-US");
    private bool _isInitialized;

    public IAppLanguage? LanguageOverride { get; private set; }
    public IAppLanguage? RequestedLanguage { get; private set; }
    public IAppLanguage? StartupLanguage { get; private set; }
    public IAppLanguage? DefaultLanguage => _defaultLanguage;
    public IAppLanguage[] SupportedLanguages { get; }

    public UiTestLocalizationService()
    {
        SupportedLanguages = [_defaultLanguage];
    }

    public void Initialize(string languageOverride)
    {
        if (_isInitialized)
        {
            return;
        }

        LanguageOverride = string.IsNullOrWhiteSpace(languageOverride)
            ? null
            : new UiTestAppLanguage(languageOverride);
        StartupLanguage = LanguageOverride ?? _defaultLanguage;
        RequestedLanguage = StartupLanguage;
        _isInitialized = true;
    }

    public string GetString(string resourceKey)
    {
        return resourceKey;
    }

    public void OverrideLanguage(IAppLanguage? language)
    {
        LanguageOverride = language;
        RequestedLanguage = language ?? _defaultLanguage;
    }

    private sealed record UiTestAppLanguage(string Value) : IAppLanguage;
}
