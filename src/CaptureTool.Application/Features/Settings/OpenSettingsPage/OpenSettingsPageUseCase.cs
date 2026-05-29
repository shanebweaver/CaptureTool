using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Settings.OpenSettingsPage;

public sealed class OpenSettingsPageUseCase : IUseCase<OpenSettingsPageRequest, OpenSettingsPageResponse>, IConditional<OpenSettingsPageRequest>
{
    private readonly INavigationService _navigationService;

    public OpenSettingsPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<bool> CanExecuteAsync(OpenSettingsPageRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<OpenSettingsPageResponse> ExecuteAsync(OpenSettingsPageRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.Settings);
        return Task.FromResult(new OpenSettingsPageResponse());
    }
}