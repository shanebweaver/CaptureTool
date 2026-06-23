using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.AudioCapture;

namespace CaptureTool.Application.Features.Home.ShowHomePage;

public sealed class ShowHomePageUseCase : IShowHomePageUseCase
{
    private const string ActivityId = "ShowHomePage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public ShowHomePageUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard? audioCaptureNavigationGuard = null)
    {
        _navigationService = navigationService;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard ?? new AllowAudioCaptureNavigationGuard();
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
