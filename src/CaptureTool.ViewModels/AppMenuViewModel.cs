using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Core;
using CaptureTool.Services.AppController;
using CaptureTool.Services.Navigation;
using CaptureTool.ViewModels.Commands;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;

    public RelayCommand NewDesktopCaptureCommand => new(NewDesktopCapture);
    public RelayCommand NewVideoCaptureCommand => new(NewVideoCapture);
    public RelayCommand NewAudioCaptureCommand => new(NewAudioCapture);
    public RelayCommand GoToSettingsCommand => new(GoToSettings);
    public RelayCommand GoToAboutCommand => new(GoToAbout);
    public RelayCommand ExitApplicationCommand => new(ExitApplication);

    public AppMenuViewModel(
        IAppController appController,
        INavigationService navigationService)
    {
        _appController = appController;
        _navigationService = navigationService;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();
        return base.LoadAsync(parameter, cancellationToken);
    }

    override public void Unload()
    {
        base.Unload();
    }

    private void NewDesktopCapture()
    {
        _appController.NewDesktopCapture();
    }

    private void NewVideoCapture()
    {
        _appController.NewVideoCapture();
    }

    private void NewAudioCapture()
    {
        _appController.NewAudioCapture();
    }

    private void GoToSettings()
    {
        _navigationService.Navigate(NavigationKeys.Settings);
    }

    private void GoToAbout()
    {
        _navigationService.Navigate(NavigationKeys.About);
    }

    private void ExitApplication()
    {
        _appController.Shutdown();
    }
}
