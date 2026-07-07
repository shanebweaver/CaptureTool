using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.Shell.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Shell.Home.ShowHomePage;

internal sealed class ShowHomePageUseCase : IShowHomePageUseCase
{
    private const string ActivityId = "ShowHomePage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public ShowHomePageUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard audioCaptureNavigationGuard)
    {
        _navigationService = navigationService;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<ShowHomePageResponse>> ExecuteAsync(ShowHomePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new ShowHomePageResponse(false);
                }

                _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
                return new ShowHomePageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
