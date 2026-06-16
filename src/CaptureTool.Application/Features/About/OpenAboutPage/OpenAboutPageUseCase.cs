using CaptureTool.Application.Abstractions.Features.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.About.OpenAboutPage;

public sealed class OpenAboutPageUseCase : IOpenAboutPageUseCase
{
    private const string ActivityId = "OpenAboutPage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public OpenAboutPageUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationService = navigationService;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<OpenAboutPageResponse>> ExecuteAsync(OpenAboutPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _navigationService.Navigate(NavigationRoute.About);
                return new OpenAboutPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
