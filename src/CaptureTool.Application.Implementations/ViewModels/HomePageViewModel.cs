using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Application.Interfaces.Actions.Home;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Application.Implementations.ViewModels.Helpers;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase, IHomePageViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string NewImageCapture = "NewImageCapture";
        public static readonly string NewVideoCapture = "NewVideoCapture";
    }

    private const string TelemetryContext = "HomePage";

    private readonly IHomeNewImageCaptureAction _newImageCaptureAction;
    private readonly IHomeNewVideoCaptureAction _newVideoCaptureAction;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand NewImageCaptureCommand { get; }
    public RelayCommand NewVideoCaptureCommand { get; }

    public bool IsVideoCaptureEnabled { get; }

    public HomePageViewModel(
        IHomeNewImageCaptureAction newImageCaptureAction,
        IHomeNewVideoCaptureAction newVideoCaptureAction,
        IFeatureManager featureManager,
        ITelemetryService telemetryService)
    {
        _newImageCaptureAction = newImageCaptureAction;
        _newVideoCaptureAction = newVideoCaptureAction;
        _telemetryService = telemetryService;

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);

        TelemetryCommandFactory commandFactory = new(telemetryService, TelemetryContext);
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
