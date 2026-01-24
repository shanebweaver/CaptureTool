using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.UseCases.Home;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;

namespace CaptureTool.Application.Implementations.UseCases.Home;

public sealed partial class HomeNewVideoCaptureUseCase : UseCase, IHomeNewVideoCaptureUseCase
{
    private readonly IAppNavigation _appNavigation;
    private readonly IFeatureManager _featureManager;
    public HomeNewVideoCaptureUseCase(IAppNavigation appNavigation, IFeatureManager featureManager)
    {
        _appNavigation = appNavigation;
        _featureManager = featureManager;
    }

    public override bool CanExecute()
    {
        return _featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
    }

    public override void Execute()
    {
        _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault);
    }
}
