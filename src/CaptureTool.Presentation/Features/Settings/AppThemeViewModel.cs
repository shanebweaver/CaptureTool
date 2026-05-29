using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Themes;
using CaptureTool.Infrastructure.ViewModels;

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
