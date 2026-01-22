using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Interfaces.Themes;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsUpdateAppThemeUseCase : ActionCommand<int>, ISettingsUpdateAppThemeUseCase
{
    private readonly IThemeService _themes;
    public SettingsUpdateAppThemeUseCase(IThemeService themes)
    {
        _themes = themes;
    }

    public override void Execute(int parameter)
    {
        var supported = new[] { AppTheme.Light, AppTheme.Dark, AppTheme.SystemDefault };
        if (parameter < 0 || parameter >= supported.Length)
        {
            return;
        }
        _themes.UpdateCurrentTheme(supported[parameter]);
    }
}
