using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.OpenSettingsPage;

public sealed class OpenSettingsPageUseCase : IOpenSettingsPageUseCase
{
    private const string ActivityId = "OpenSettingsPage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;

    public OpenSettingsPageUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
    }

    public bool CanExecute(OpenSettingsPageRequest request) => true;

    public Task<UseCaseResponse<OpenSettingsPageResponse>> ExecuteAsync(OpenSettingsPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _navigationService.Navigate(NavigationRoute.Settings);
                return new OpenSettingsPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
