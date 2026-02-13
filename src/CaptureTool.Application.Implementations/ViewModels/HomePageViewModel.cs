using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.UseCases.Home;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Implementations.UseCases.Extensions;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase, IHomePageViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string NewImageCapture = "NewImageCapture";
        public static readonly string NewVideoCapture = "NewVideoCapture";
        public static readonly string NewAudioCapture = "NewAudioCapture";
    }

    private const string TelemetryContext = "HomePage";

    private readonly IHomeNewImageCaptureUseCase _newImageCaptureAction;
    private readonly IHomeNewVideoCaptureUseCase _newVideoCaptureAction;
    private readonly IHomeNewAudioCaptureUseCase _newAudioCaptureAction;

    public IAppCommand NewImageCaptureCommand { get; }
    public IAppCommand NewVideoCaptureCommand { get; }
    public IAppCommand NewAudioCaptureCommand { get; }

    public bool IsVideoCaptureEnabled { get; }
    public bool IsAudioCaptureEnabled { get; }

    public HomePageViewModel(
        IHomeNewImageCaptureUseCase newImageCaptureAction,
        IHomeNewVideoCaptureUseCase newVideoCaptureAction,
        IHomeNewAudioCaptureUseCase newAudioCaptureAction,
        IFeatureManager featureManager,
        ITelemetryService telemetryService)
    {
        _newImageCaptureAction = newImageCaptureAction;
        _newVideoCaptureAction = newVideoCaptureAction;
        _newAudioCaptureAction = newAudioCaptureAction;

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
        IsAudioCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_AudioCapture);

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        NewImageCaptureCommand = commandFactory.Create(ActivityIds.NewImageCapture, NewImageCapture);
        NewVideoCaptureCommand = commandFactory.Create(ActivityIds.NewVideoCapture, NewVideoCapture, () => IsVideoCaptureEnabled);
        NewAudioCaptureCommand = commandFactory.Create(ActivityIds.NewAudioCapture, NewAudioCapture, () => IsAudioCaptureEnabled);
    }

    private void NewImageCapture()
    {
        _newImageCaptureAction.ExecuteCommand();
    }

    private void NewVideoCapture()
    {
        _newVideoCaptureAction.ExecuteCommand();
    }

    private void NewAudioCapture()
    {
        _newAudioCaptureAction.ExecuteCommand();
    }
}
