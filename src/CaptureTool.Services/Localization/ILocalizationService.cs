namespace CaptureTool.Services.Localization;

public interface ILocalizationService
{
    AppLanguage? LanguageOverride { get; }
    AppLanguage RequestedLanguage { get; }
    AppLanguage StartupLanguage { get; }
    AppLanguage DefaultLanguage { get; }
    AppLanguage[] SupportedLanguages { get; }

    string GetString(string resourceKey);
    void OverrideLanguage(AppLanguage? language);
}
