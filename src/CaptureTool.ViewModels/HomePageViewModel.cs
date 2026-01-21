using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Core.Interfaces.Actions.Home;
using CaptureTool.Core.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels.Helpers;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
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

        NewImageCaptureCommand = new(NewImageCapture);
        NewVideoCaptureCommand = new(NewVideoCapture, () => IsVideoCaptureEnabled);

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
    }

    private void NewImageCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, TelemetryContext, ActivityIds.NewImageCapture, () =>
        {
            _newImageCaptureAction.ExecuteCommand();
        });
    }

    private void NewVideoCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, TelemetryContext, ActivityIds.NewVideoCapture, () =>
        {
            _newVideoCaptureAction.ExecuteCommand();
        });
    }
}
