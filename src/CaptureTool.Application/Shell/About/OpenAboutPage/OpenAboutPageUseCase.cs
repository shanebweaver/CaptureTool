using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shell.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Shell.About.OpenAboutPage;

internal sealed class OpenAboutPageUseCase : IOpenAboutPageUseCase
{
    private const string ActivityId = "OpenAboutPage";

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public OpenAboutPageUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationCoordinator = navigationCoordinator;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<OpenAboutPageResponse>> ExecuteAsync(OpenAboutPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.About,
                    cancellationToken: cancellationToken);
                return new OpenAboutPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
