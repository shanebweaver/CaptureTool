using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Themes;

namespace CaptureTool.Application.UseCases.Settings;

internal class SettingsUpdateAppThemeAppCommand : ISettingsUpdateAppThemeAppCommand
{
    private readonly IThemeService _themes;
    public SettingsUpdateAppThemeAppCommand(IThemeService themes)
    {
        _themes = themes;
    }

    public bool CanExecute(int parameter)
    {
        return parameter >= 0 && parameter <= 2;
    }

    public void Execute(int parameter)
    {
        var supported = new[] { AppTheme.Light, AppTheme.Dark, AppTheme.SystemDefault };
        if (parameter < 0 || parameter >= supported.Length)
        {
            return;
        }
        _themes.UpdateCurrentTheme(supported[parameter]);
    }
}
