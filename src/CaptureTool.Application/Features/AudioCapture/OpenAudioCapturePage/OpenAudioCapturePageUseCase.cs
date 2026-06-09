using CaptureTool.Application.Abstractions.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.AudioCapture.OpenAudioCapturePage;

public sealed class OpenAudioCapturePageUseCase : IOpenAudioCapturePageUseCase
{
    private readonly INavigationService _navigationService;

    public OpenAudioCapturePageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<OpenAudioCapturePageResponse> ExecuteAsync(OpenAudioCapturePageRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.AudioCapture);
        return Task.FromResult(new OpenAudioCapturePageResponse());
    }
}