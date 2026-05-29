using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Themes;

namespace CaptureTool.Application.Features.Settings.UpdateAppTheme;

public sealed class UpdateAppThemeUseCase : IUseCase<UpdateAppThemeRequest, UpdateAppThemeResponse>, IConditional<UpdateAppThemeRequest>
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
        if (request.ThemeIndex >= 0 && request.ThemeIndex < SupportedThemes.Length)
        {
            _themes.UpdateCurrentTheme(SupportedThemes[request.ThemeIndex]);
        }

        return Task.FromResult(new UpdateAppThemeResponse());
    }
}