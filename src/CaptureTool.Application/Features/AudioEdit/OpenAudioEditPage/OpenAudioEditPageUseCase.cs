using CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;

public sealed class OpenAudioEditPageUseCase : IOpenAudioEditPageUseCase
{
    private readonly INavigationService _navigationService;

    public OpenAudioEditPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanExecute(OpenAudioEditPageRequest request)
    {
        bool canExecute = File.Exists(request.AudioFile.FilePath);
        return canExecute;
    }

    public Task<OpenAudioEditPageResponse> ExecuteAsync(OpenAudioEditPageRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.AudioEdit, request.AudioFile);
        return Task.FromResult(new OpenAudioEditPageResponse());
    }
}
