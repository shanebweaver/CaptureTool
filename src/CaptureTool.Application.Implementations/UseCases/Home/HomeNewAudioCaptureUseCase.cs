using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.UseCases.Home;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;

namespace CaptureTool.Application.Implementations.UseCases.Home;

public sealed partial class HomeNewAudioCaptureUseCase : UseCase, IHomeNewAudioCaptureUseCase
{
    private readonly IAppNavigation _appNavigation;
    private readonly IFeatureManager _featureManager;
    public HomeNewAudioCaptureUseCase(IAppNavigation appNavigation, IFeatureManager featureManager)
    {
        _appNavigation = appNavigation;
        _featureManager = featureManager;
    }

    public override bool CanExecute()
    {
        return _featureManager.IsEnabled(CaptureToolFeatures.Feature_AudioCapture);
    }

    public override void Execute()
    {
        _appNavigation.GoToAudioCapture();
    }
}
