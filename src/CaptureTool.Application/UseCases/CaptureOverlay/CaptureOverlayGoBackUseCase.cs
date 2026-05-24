using CaptureTool.Application.Services.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Navigation;

namespace CaptureTool.Application.UseCases.CaptureOverlay;

public sealed partial class CaptureOverlayGoBackUseCase : UseCase, ICaptureOverlayGoBackUseCase
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;

    public CaptureOverlayGoBackUseCase(
        IVideoCaptureHandler videoCaptureHandler,
        INavigationService navigationService,
        IAppNavigation appNavigation)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _navigationService = navigationService;
        _appNavigation = appNavigation;
    }

    public override bool CanExecute()
    {
        if (!_navigationService.CanGoBack)
        {
            return false;
        }

        if (_navigationService.CurrentRequest?.Route is not CaptureToolNavigationRoute route
            || route != CaptureToolNavigationRoute.VideoCapture)
        {
            return false;
        }

        return true;
    }

    public override void Execute()
    {
        try
        {
            _videoCaptureHandler.CancelVideoCapture();
        }
        catch { }

        if (!_appNavigation.TryGoBack())
        {
            _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault, true);
        }
    }
}
