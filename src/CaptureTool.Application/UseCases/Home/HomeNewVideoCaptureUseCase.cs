using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases.Home;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.UseCases.Home;

public sealed partial class HomeNewVideoCaptureUseCase : UseCase, IHomeNewVideoCaptureUseCase
{
    private readonly IAppNavigation _appNavigation;

    public HomeNewVideoCaptureUseCase(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault);
    }
}
