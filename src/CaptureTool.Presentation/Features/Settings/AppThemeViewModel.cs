using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Presentation.ViewModels;

namespace CaptureTool.Presentation.Features.Settings;

public sealed partial class AppThemeViewModel : ViewModelBase
{
    public AppTheme AppTheme { get; }
    public string DisplayName { get; }
    public string AutomationName { get; }

    public AppThemeViewModel(
        AppTheme appTheme,
        ILocalizationService localizationService)
    {
        AppTheme = appTheme;
        DisplayName = localizationService.GetString($"AppTheme_{Enum.GetName(appTheme)}");
        AutomationName = DisplayName;
    }
}
