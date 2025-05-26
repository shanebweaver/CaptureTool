using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Navigation;
using System.Drawing;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopImageCaptureWindowViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly INavigationService _navigationService;

    public RelayCommand GoBackCommand => new(GoBack);

    private Rectangle _captureArea;
    public Rectangle CaptureArea
    {
        get => _captureArea;
        set => Set(ref _captureArea, value);
    }

    public DesktopImageCaptureWindowViewModel(
        IAppController appController,
        INavigationService navigationService)
    {
        _appController = appController;
        _navigationService = navigationService;

        _captureArea = new(100, 100, 200, 300);
    }

    private void GoBack()
    {
        _appController.GoBackOrHome();
    }
}
