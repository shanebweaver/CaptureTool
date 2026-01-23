using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.UseCases.Home;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Common;
using CaptureTool.Infrastructure.Implementations.UseCases.Extensions;
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
    }

    private const string TelemetryContext = "HomePage";

    private readonly IHomeNewImageCaptureUseCase _newImageCaptureAction;
    private readonly IHomeNewVideoCaptureUseCase _newVideoCaptureAction;

    public IAppCommand NewImageCaptureCommand { get; }
    public IAppCommand NewVideoCaptureCommand { get; }

    public bool IsVideoCaptureEnabled { get; }

    public HomePageViewModel(
        IHomeNewImageCaptureUseCase newImageCaptureAction,
        IHomeNewVideoCaptureUseCase newVideoCaptureAction,
        IFeatureManager featureManager,
        ITelemetryService telemetryService)
    {
        _newImageCaptureAction = newImageCaptureAction;
        _newVideoCaptureAction = newVideoCaptureAction;

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        NewImageCaptureCommand = commandFactory.Create(ActivityIds.NewImageCapture, NewImageCapture);
        NewVideoCaptureCommand = commandFactory.Create(ActivityIds.NewVideoCapture, NewVideoCapture, () => IsVideoCaptureEnabled);
    }

    private void NewImageCapture()
    {
        _newImageCaptureAction.ExecuteCommand();
    }

    private void NewVideoCapture()
    {
        _newVideoCaptureAction.ExecuteCommand();
    }
}
