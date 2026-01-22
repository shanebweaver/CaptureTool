using CaptureTool.Infrastructure.Interfaces.Themes;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAppThemeViewModel
{
    AppTheme AppTheme { get; }
    string DisplayName { get; }
    string AutomationName { get; }
}
