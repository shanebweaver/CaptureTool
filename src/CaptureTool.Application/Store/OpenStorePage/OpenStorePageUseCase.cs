using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Store.OpenStorePage;

internal sealed class OpenStorePageUseCase : IOpenStorePageUseCase
{
    private const string ActivityId = "OpenStorePage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationCoordinator _navigationCoordinator;

    public OpenStorePageUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationCoordinator = navigationCoordinator;
    }

    public Task<UseCaseResponse<OpenStorePageResponse>> ExecuteAsync(OpenStorePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.Store,
                    cancellationToken: cancellationToken);
                return new OpenStorePageResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
