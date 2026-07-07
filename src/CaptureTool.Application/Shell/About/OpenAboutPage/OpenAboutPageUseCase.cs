using CaptureTool.Application.Abstractions.Shell.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Shell.About.OpenAboutPage;

internal sealed class OpenAboutPageUseCase : IOpenAboutPageUseCase
{
    private const string ActivityId = "OpenAboutPage";

    private readonly INavigationService _navigationService;
    private readonly IEditSessionGuard _editSessionGuard;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public OpenAboutPageUseCase(
        INavigationService navigationService,
        IEditSessionGuard editSessionGuard,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard audioCaptureNavigationGuard)
    {
        _navigationService = navigationService;
        _editSessionGuard = editSessionGuard;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard;
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

                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenAboutPageResponse();
                }

                _navigationService.Navigate(NavigationRoute.About);
                return new OpenAboutPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
