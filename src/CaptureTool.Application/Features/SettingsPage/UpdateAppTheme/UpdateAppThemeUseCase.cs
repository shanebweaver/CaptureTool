using CaptureTool.Application.Abstractions.Features.Settings.UpdateAppTheme;
using CaptureTool.Application.Abstractions.Themes;

namespace CaptureTool.Application.Features.SettingsPage.UpdateAppTheme;

public sealed class UpdateAppThemeUseCase : IUpdateAppThemeUseCase
{
    private static readonly AppTheme[] SupportedThemes = [AppTheme.Light, AppTheme.Dark, AppTheme.SystemDefault];
    private readonly IThemeService _themes;

    public UpdateAppThemeUseCase(IThemeService themes)
    {
        _themes = themes;
    }

    public bool CanExecute(UpdateAppThemeRequest request)
    {
        return request.ThemeIndex >= 0 && request.ThemeIndex < SupportedThemes.Length;
    }

    public Task<UpdateAppThemeResponse> ExecuteAsync(UpdateAppThemeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.ThemeIndex >= 0 && request.ThemeIndex < SupportedThemes.Length)
            {
                _themes.UpdateCurrentTheme(SupportedThemes[request.ThemeIndex]);
            }

            return Task.FromResult(new UpdateAppThemeResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new UpdateAppThemeResponse(false));
        }
    }
}
