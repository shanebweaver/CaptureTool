using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;

public sealed class OpenVideoEditPageUseCase : IOpenVideoEditPageUseCase
{
    private const string ActivityId = "OpenVideoEditPage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public OpenVideoEditPageUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationService = navigationService;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<OpenVideoEditPageResponse>> ExecuteAsync(OpenVideoEditPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _navigationService.Navigate(NavigationRoute.VideoEdit, request.VideoFile);
                return new OpenVideoEditPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
