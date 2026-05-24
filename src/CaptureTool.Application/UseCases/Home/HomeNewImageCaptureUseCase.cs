using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases.Home;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.UseCases.Home;

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
