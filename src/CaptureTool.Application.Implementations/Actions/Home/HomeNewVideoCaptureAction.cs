using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.Home;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;

namespace CaptureTool.Application.Implementations.Actions.Home;

public sealed partial class HomeNewVideoCaptureAction : ActionCommand, IHomeNewVideoCaptureAction
{
    private readonly IAppNavigation _appNavigation;
    private readonly IFeatureManager _featureManager;
    public HomeNewVideoCaptureAction(IAppNavigation appNavigation, IFeatureManager featureManager)
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
