namespace CaptureTool.Services.Localization;

public interface ILocalizationService
{
    public string CurrentLanguage { get; }
    public string StartupLanguage { get; }
    string[] SupportedLanguages { get; }

    public string GetString(string resourceKey);

    void UpdateCurrentLanguage(string language);
}
