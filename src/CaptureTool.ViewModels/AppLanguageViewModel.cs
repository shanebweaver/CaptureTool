using CaptureTool.Core;
using CaptureTool.Services.Localization;
using System.Globalization;

namespace CaptureTool.ViewModels;

public sealed partial class AppLanguageViewModel : ViewModelBase
{
    public AppLanguage? Language { get; }
    public string DisplayName { get; }
    public string AutomationName { get; }

    public AppLanguageViewModel(
        AppLanguage? language,
        ILocalizationService localizationService)
    {
        Language = language;

        if (language == null)
        {
            string useDefaultString = localizationService.GetString("AppLanguage_SystemDefault");
            DisplayName = useDefaultString;
            AutomationName = useDefaultString;
        }
        else
        {
            CultureInfo langInfo = new(language.Value.Value);
            DisplayName = langInfo.NativeName;
            AutomationName = langInfo.NativeName;
        }
    }
}
