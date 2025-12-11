using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Home;
using CaptureTool.Core.Interfaces.FeatureManagement;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
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

    private readonly IHomeActions _homeActions;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand NewImageCaptureCommand { get; }
    public RelayCommand NewVideoCaptureCommand { get; }

    public bool IsVideoCaptureEnabled { get; }

    public HomePageViewModel(
        IHomeActions homeActions,
        IFeatureManager featureManager,
        ITelemetryService telemetryService)
    {
        _homeActions = homeActions;
        _telemetryService = telemetryService;

        NewImageCaptureCommand = new(NewImageCapture);
        NewVideoCaptureCommand = new(NewVideoCapture, () => IsVideoCaptureEnabled);

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
    }

    private void NewImageCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.NewImageCapture, () =>
        {
            _homeActions.NewImageCapture();
        });
    }

    private void NewVideoCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.NewVideoCapture, () =>
        {
            _homeActions.NewVideoCapture();
        });
    }
}
