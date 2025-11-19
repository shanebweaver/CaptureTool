using CaptureTool.Capture;
using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Telemetry;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Telemetry;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string NewImageCapture = "HomePageViewModel_NewImageCapture";
        public static readonly string NewVideoCapture = "HomePageViewModel_NewVideoCapture";
    }

    private readonly IAppNavigation _appNavigation;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand NewImageCaptureCommand { get; }
    public RelayCommand NewVideoCaptureCommand { get; }

    public bool IsVideoCaptureEnabled { get; }

    public HomePageViewModel(
        IAppNavigation appNavigation,
        IFeatureManager featureManager,
        ITelemetryService telemetryService)
    {
        _appNavigation = appNavigation;
        _telemetryService = telemetryService;

        NewImageCaptureCommand = new(NewImageCapture);
        NewVideoCaptureCommand = new(NewVideoCapture, () => IsVideoCaptureEnabled);

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
    }

    private void NewImageCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.NewImageCapture, () =>
        {
            _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
        });
    }

    private void NewVideoCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.NewVideoCapture, () =>
        {
            _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault);
        });
    }
}
