using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.SettingsPage.OpenSettingsPage;

public sealed class OpenSettingsPageUseCase : IOpenSettingsPageUseCase
{
    private readonly INavigationService _navigationService;

    public OpenSettingsPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanExecute(OpenSettingsPageRequest request) => true;

    public Task<OpenSettingsPageResponse> ExecuteAsync(OpenSettingsPageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _navigationService.Navigate(NavigationRoute.Settings);
            return Task.FromResult(new OpenSettingsPageResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new OpenSettingsPageResponse(false));
        }
    }
}
