using CaptureTool.Common;
using CaptureTool.Services.Interfaces.Localization;
using System.Globalization;

namespace CaptureTool.ViewModels;

public sealed partial class AppLanguageViewModel : ViewModelBase
{
    public IAppLanguage? Language { get; }
    public string DisplayName { get; }
    public string AutomationName { get; }

    public AppLanguageViewModel(
        IAppLanguage? language,
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
            CultureInfo langInfo = new(language.Value);
            DisplayName = langInfo.NativeName;
            AutomationName = langInfo.NativeName;
        }
    }
}
