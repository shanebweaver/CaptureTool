using CaptureTool.Application.Abstractions.Features.Settings.UpdateAppTheme;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.UpdateAppTheme;

public sealed class UpdateAppThemeUseCase : IUpdateAppThemeUseCase
{
    private const string ActivityId = "UpdateAppTheme";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private static readonly AppTheme[] SupportedThemes = [AppTheme.Light, AppTheme.Dark, AppTheme.SystemDefault];
    private readonly IThemeService _themes;

    public UpdateAppThemeUseCase(IThemeService themes,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _themes = themes;
    }

    public bool CanExecute(UpdateAppThemeRequest request)
    {
        return request.ThemeIndex >= 0 && request.ThemeIndex < SupportedThemes.Length;
    }

    public Task<UseCaseResponse<UpdateAppThemeResponse>> ExecuteAsync(UpdateAppThemeRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                if (request.ThemeIndex >= 0 && request.ThemeIndex < SupportedThemes.Length)
                {
                    _themes.UpdateCurrentTheme(SupportedThemes[request.ThemeIndex]);
                }

                return new UpdateAppThemeResponse();
            },
            cancellationToken: cancellationToken);
    }
}
