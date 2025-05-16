namespace CaptureTool.Services.Localization;

public interface ILocalizationService
{
    public AppLanguage CurrentLanguage { get; }
    public AppLanguage StartupLanguage { get; }
    AppLanguage[] SupportedLanguages { get; }

    public string GetString(string resourceKey);

    void UpdateCurrentLanguage(AppLanguage language);
}
