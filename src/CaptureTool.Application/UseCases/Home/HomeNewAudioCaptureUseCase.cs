using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases.Home;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.UseCases;

namespace CaptureTool.Application.UseCases.Home;

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
