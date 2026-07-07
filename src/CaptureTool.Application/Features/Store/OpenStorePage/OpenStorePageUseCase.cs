using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.Store.OpenStorePage;

internal sealed class OpenStorePageUseCase : IOpenStorePageUseCase
{
    private const string ActivityId = "OpenStorePage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenStorePageUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard audioCaptureNavigationGuard)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard;
    }

    public Task<UseCaseResponse<OpenStorePageResponse>> ExecuteAsync(OpenStorePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenStorePageResponse(false);
                }

                _navigationService.Navigate(NavigationRoute.Store);
                return new OpenStorePageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
