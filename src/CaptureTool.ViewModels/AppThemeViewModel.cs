using CaptureTool.Services.Localization;
using CaptureTool.Services.Themes;
using System;

namespace CaptureTool.ViewModels;

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
