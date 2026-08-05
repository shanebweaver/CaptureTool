using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Audio.OpenAudioCapturePage;

internal sealed class OpenAudioCapturePageUseCase : IOpenAudioCapturePageUseCase
{
    private const string ActivityId = "OpenAudioCapturePage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationCoordinator _navigationCoordinator;

    public OpenAudioCapturePageUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationCoordinator = navigationCoordinator;
    }

    public Task<UseCaseResponse<OpenAudioCapturePageResponse>> ExecuteAsync(OpenAudioCapturePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.AudioCapture,
                    cancellationToken: cancellationToken);
                return new OpenAudioCapturePageResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
