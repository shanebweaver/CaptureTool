using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Infrastructure.Interfaces.Themes;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsUpdateAppThemeAction : ActionCommand<int>, ISettingsUpdateAppThemeAction
{
    private readonly IThemeService _themes;
    public SettingsUpdateAppThemeAction(IThemeService themes)
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
