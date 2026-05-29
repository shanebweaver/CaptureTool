using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.AudioCapture.OpenAudioCapturePage;

public sealed class OpenAudioCapturePageUseCase : IUseCase<OpenAudioCapturePageRequest, OpenAudioCapturePageResponse>
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