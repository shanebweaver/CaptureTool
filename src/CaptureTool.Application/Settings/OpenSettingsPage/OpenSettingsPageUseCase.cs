using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.OpenSettingsPage;

internal sealed class OpenSettingsPageUseCase : IOpenSettingsPageUseCase
{
    private const string ActivityId = "OpenSettingsPage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationCoordinator _navigationCoordinator;

    public OpenSettingsPageUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationCoordinator = navigationCoordinator;
    }

    public bool CanExecute(OpenSettingsPageRequest request) => true;

    public Task<UseCaseResponse<OpenSettingsPageResponse>> ExecuteAsync(OpenSettingsPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.Settings,
                    cancellationToken: cancellationToken);
                return new OpenSettingsPageResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
