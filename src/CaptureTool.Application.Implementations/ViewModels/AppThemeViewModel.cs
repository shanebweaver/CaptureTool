using CaptureTool.Common;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Themes;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class AppThemeViewModel : ViewModelBase, IAppThemeViewModel
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
