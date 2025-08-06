using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Telemetry;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "AppMenuViewModel_Load";
        public static readonly string Unload = "AppMenuViewModel_Unload";
        public static readonly string NewImageCapture = "AppMenuViewModel_NewImageCapture";
        public static readonly string OpenFile = "AppMenuViewModel_OpenFile";
        public static readonly string NavigateToSettings = "AppMenuViewModel_NavigateToSettings";
        public static readonly string ShowAboutApp = "AppMenuViewModel_ShowAboutApp";
        public static readonly string ShowAddOns = "AppMenuViewModel_ShowAddOns";
        public static readonly string ExitApplication = "AppMenuViewModel_ExitApplication";
        public static readonly string SendFeedback = "AppMenuViewModel_SendFeedback";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;
    private readonly IFilePickerService _filePickerService;

    public RelayCommand NewImageCaptureCommand => new(NewImageCapture);
    public RelayCommand OpenFileCommand => new(OpenFile);
    public RelayCommand NavigateToSettingsCommand => new(NavigateToSettings);
    public RelayCommand ShowAboutAppCommand => new(ShowAboutApp); 
    public RelayCommand ShowAddOnsCommand => new(ShowAddOns); 
    public RelayCommand ExitApplicationCommand => new(ExitApplication);

    public AppMenuViewModel(
        ITelemetryService telemetryService,
        IAppController appController,
        INavigationService navigationService,
        IFilePickerService filePickerService)
    {
        _telemetryService = telemetryService;
        _appController = appController;
        _navigationService = navigationService;
        _filePickerService = filePickerService;
    }

    private void NewImageCapture()
    {
        string activityId = ActivityIds.NewImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _appController.ShowCaptureOverlay();
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

            _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile, true);
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
            _navigationService.Navigate(CaptureToolNavigationRoutes.Settings);
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
            _navigationService.Navigate(CaptureToolNavigationRoutes.About);
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
            _navigationService.Navigate(CaptureToolNavigationRoutes.AddOns);
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
