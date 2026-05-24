using CaptureTool.Application.Abstractions.UseCases.Settings;
using CaptureTool.Infrastructure.UseCases;
using CaptureTool.Infrastructure.Abstractions.Themes;

namespace CaptureTool.Application.UseCases.Settings;

public sealed partial class SettingsUpdateAppThemeUseCase : UseCase<int>, ISettingsUpdateAppThemeUseCase
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
