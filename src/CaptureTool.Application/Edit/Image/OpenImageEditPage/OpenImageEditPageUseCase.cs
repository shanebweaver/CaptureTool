using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Edit.Image.OpenImageEditPage;

internal sealed class OpenImageEditPageUseCase : IOpenImageEditPageUseCase
{
    private const string ActivityId = "OpenImageEditPage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenImageEditPageUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard audioCaptureNavigationGuard)
    {
        _navigationService = navigationService;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(OpenImageEditPageRequest request)
    {
        return true;
    }

    public Task<UseCaseResponse<OpenImageEditPageResponse>> ExecuteAsync(OpenImageEditPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenImageEditPageResponse(false);
                }

                _navigationService.Navigate(NavigationRoute.ImageEdit, request.ImageFile);
                return new OpenImageEditPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
