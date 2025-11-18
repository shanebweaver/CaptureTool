using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Telemetry;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "AppMenuViewModel_Load";
        public static readonly string Dispose = "AppMenuViewModel_Dispose";
        public static readonly string NewImageCapture = "AppMenuViewModel_NewImageCapture";
        public static readonly string OpenFile = "AppMenuViewModel_OpenFile";
        public static readonly string NavigateToSettings = "AppMenuViewModel_NavigateToSettings";
        public static readonly string ShowAboutApp = "AppMenuViewModel_ShowAboutApp";
        public static readonly string ShowAddOns = "AppMenuViewModel_ShowAddOns";
        public static readonly string ExitApplication = "AppMenuViewModel_ExitApplication";
        public static readonly string SendFeedback = "AppMenuViewModel_SendFeedback";
    }

    private readonly IAppNavigation _appNavigation;
    private readonly IAppController _appController;
    private readonly ITelemetryService _telemetryService;
    private readonly IFilePickerService _filePickerService;

    public RelayCommand NewImageCaptureCommand => new(NewImageCapture);
    public RelayCommand OpenFileCommand => new(OpenFile);
    public RelayCommand NavigateToSettingsCommand => new(NavigateToSettings);
    public RelayCommand ShowAboutAppCommand => new(ShowAboutApp); 
    public RelayCommand ShowAddOnsCommand => new(ShowAddOns); 
    public RelayCommand ExitApplicationCommand => new(ExitApplication);

    public bool ShowAddOnsOption { get; }

    public AppMenuViewModel(
        IAppNavigation appNavigation,
        ITelemetryService telemetryService,
        IAppController appController,
        IFilePickerService filePickerService,
        IFeatureManager featureManager)
    {
        _appNavigation = appNavigation;
        _telemetryService = telemetryService;
        _appController = appController;
        _filePickerService = filePickerService;

        ShowAddOnsOption = featureManager.IsEnabled(CaptureToolFeatures.Feature_AddOns_Store);
    }

    private void NewImageCapture()
    {
        string activityId = ActivityIds.NewImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private async void OpenFile()
    {
        string activityId = ActivityIds.OpenFile;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            nint hwnd = _appController.GetMainWindowHandle();
            var imageFile = await _filePickerService.OpenImageFileAsync(hwnd);
            if (imageFile == null)
            {
                _telemetryService.ActivityCanceled(activityId);
                return;
            }

            _appNavigation.GoToImageEdit(imageFile);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void NavigateToSettings()
    {
        string activityId = ActivityIds.NavigateToSettings;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _appNavigation.GoToSettings();
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void ShowAboutApp()
    {
        string activityId = ActivityIds.ShowAboutApp;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _appNavigation.GoToAbout();
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void ShowAddOns()
    {
        string activityId = ActivityIds.ShowAddOns;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _appNavigation.GoToAddOns();
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void ExitApplication()
    {
        string activityId = ActivityIds.ExitApplication;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _appController.Shutdown();
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
