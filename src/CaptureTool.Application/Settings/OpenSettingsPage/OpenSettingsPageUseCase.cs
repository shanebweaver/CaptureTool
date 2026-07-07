using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.OpenSettingsPage;

internal sealed class OpenSettingsPageUseCase : IOpenSettingsPageUseCase
{
    private const string ActivityId = "OpenSettingsPage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IEditSessionGuard _editSessionGuard;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenSettingsPageUseCase(INavigationService navigationService,
        IEditSessionGuard editSessionGuard,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard audioCaptureNavigationGuard)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _editSessionGuard = editSessionGuard;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard;
    }

    public bool CanExecute(OpenSettingsPageRequest request) => true;

    public Task<UseCaseResponse<OpenSettingsPageResponse>> ExecuteAsync(OpenSettingsPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _editSessionGuard.CanLeaveCurrentSessionAsync(cancellationToken))
                {
                    return new OpenSettingsPageResponse();
                }

                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenSettingsPageResponse(false);
                }

                _navigationService.Navigate(NavigationRoute.Settings);
                return new OpenSettingsPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
