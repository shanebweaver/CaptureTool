using CaptureTool.Application.Abstractions.UseCases.Home;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.UseCases.Extensions;
using CaptureTool.Infrastructure.ViewModels;
using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Presentation.ViewModels.Helpers;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
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

        IsAudioCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_AudioCapture);

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        NewImageCaptureCommand = commandFactory.Create(ActivityIds.NewImageCapture, NewImageCapture);
        NewVideoCaptureCommand = commandFactory.Create(ActivityIds.NewVideoCapture, NewVideoCapture);
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
