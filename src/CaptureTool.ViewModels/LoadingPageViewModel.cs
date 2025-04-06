using CaptureTool.Services.Navigation;
using CaptureTool.ViewModels.Commands;

namespace CaptureTool.ViewModels;

public class LoadingPageViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public RelayCommand GoBackCommand => new(GoBack);

    public LoadingPageViewModel(
        INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
