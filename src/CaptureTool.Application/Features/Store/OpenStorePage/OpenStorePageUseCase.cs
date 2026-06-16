using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Store.OpenStorePage;

public sealed class OpenStorePageUseCase : IOpenStorePageUseCase
{
    private const string ActivityId = "OpenStorePage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;

    public OpenStorePageUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
    }

    public Task<UseCaseResponse<OpenStorePageResponse>> ExecuteAsync(OpenStorePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _navigationService.Navigate(NavigationRoute.Store);
                return new OpenStorePageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
