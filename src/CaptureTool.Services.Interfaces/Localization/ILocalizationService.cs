namespace CaptureTool.Services.Interfaces.Localization;

public interface ILocalizationService
{
    IAppLanguage? LanguageOverride { get; }
    IAppLanguage? RequestedLanguage { get; }
    IAppLanguage? StartupLanguage { get; }
    IAppLanguage? DefaultLanguage { get; }
    IAppLanguage[] SupportedLanguages { get; }

    void Initialize(string languageOverride);

    string GetString(string resourceKey);
    void OverrideLanguage(IAppLanguage? language);
}
