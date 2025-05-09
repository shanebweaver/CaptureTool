namespace CaptureTool.Services.Localization;

public interface ILocalizationService
{
    string[] SupportedLanguages { get; }

    public string GetString(string resourceKey);

    void UpdatePrimaryLanguage(string language);
}
