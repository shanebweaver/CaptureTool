using CaptureTool.Application.Abstractions.Features.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.About.OpenAboutPage;

public sealed class OpenAboutPageUseCase : IOpenAboutPageUseCase
{
    private const string ActivityId = "OpenAboutPage";

    private readonly INavigationService _navigationService;
    private readonly IEditSessionGuard _editSessionGuard;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public OpenAboutPageUseCase(
        INavigationService navigationService,
        IEditSessionGuard editSessionGuard,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationService = navigationService;
        _editSessionGuard = editSessionGuard;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<OpenAboutPageResponse>> ExecuteAsync(OpenAboutPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _editSessionGuard.CanLeaveCurrentSessionAsync(cancellationToken))
                {
                    return new OpenAboutPageResponse();
                }

                _navigationService.Navigate(NavigationRoute.About);
                return new OpenAboutPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
