using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.UseCases;
using CaptureTool.Application.Interfaces.UseCases.Home;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Implementations.UseCases.Home;

public sealed partial class HomeNewImageCaptureUseCase : UseCase, IHomeNewImageCaptureUseCase
{
    private readonly IAppNavigation _appNavigation;
    public HomeNewImageCaptureUseCase(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
    }
}
