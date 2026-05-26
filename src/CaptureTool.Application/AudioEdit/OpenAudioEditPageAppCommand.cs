using CaptureTool.Application.Abstractions.AudioEdit;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.AudioEdit;

internal class OpenAudioEditPageAppCommand : IOpenAudioEditPageAppCommand
{
    private readonly INavigationService _navigationService;

    public OpenAudioEditPageAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanExecute(IAudioFile parameter)
    {
        return true;
    }

    public void Execute(IAudioFile parameter)
    {
        _navigationService.Navigate(NavigationRoute.AudioEdit, parameter);
    }
}
