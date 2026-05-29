using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;

public sealed class OpenAudioEditPageUseCase : IUseCase<OpenAudioEditPageRequest, OpenAudioEditPageResponse>, IConditional<OpenAudioEditPageRequest>
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
