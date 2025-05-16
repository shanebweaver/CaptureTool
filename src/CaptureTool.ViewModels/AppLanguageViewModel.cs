using CaptureTool.Services.Localization;
using System.Globalization;

namespace CaptureTool.ViewModels;

public sealed partial class AppLanguageViewModel : ViewModelBase
{
    public AppLanguage Language { get; }
    public string DisplayName { get; }

    public AppLanguageViewModel(AppLanguage language)
    {
        Language = language;

        CultureInfo langInfo = new(language.Value);
        DisplayName = langInfo.NativeName;
    }
}
