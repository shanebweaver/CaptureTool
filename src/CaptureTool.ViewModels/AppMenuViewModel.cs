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

    public RelayCommand NewImageCaptureCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand NavigateToSettingsCommand { get; }
    public RelayCommand ShowAboutAppCommand { get; }
    public RelayCommand ShowAddOnsCommand { get; }
    public RelayCommand ExitApplicationCommand { get; }

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

        NewImageCaptureCommand = new RelayCommand(NewImageCapture);
        OpenFileCommand = new RelayCommand(OpenFile);
        NavigateToSettingsCommand = new RelayCommand(NavigateToSettings);
        ShowAboutAppCommand = new RelayCommand(ShowAboutApp);
        ShowAddOnsCommand = new RelayCommand(ShowAddOns);
        ExitApplicationCommand = new RelayCommand(ExitApplication);

        ShowAddOnsOption = featureManager.IsEnabled(CaptureToolFeatures.Feature_AddOns_Store);
    }

    private void NewImageCapture()
    {
        _telemetryService.ExecuteActivity(ActivityIds.NewImageCapture, async () =>
        {
            _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
        });
    }

    private async void OpenFile()
    {
        _telemetryService.ExecuteActivity(ActivityIds.OpenFile, async () =>
        {
            nint hwnd = _appController.GetMainWindowHandle();
            var imageFile = await _filePickerService.OpenImageFileAsync(hwnd) ?? throw new OperationCanceledException();
            _appNavigation.GoToImageEdit(imageFile);
        });
    }

    private void NavigateToSettings()
    {
        _telemetryService.ExecuteActivity(ActivityIds.NavigateToSettings, async () =>
        {
            _appNavigation.GoToSettings();
        });
    }

    private void ShowAboutApp()
    {
        _telemetryService.ExecuteActivity(ActivityIds.ShowAboutApp, async () =>
        {
            _appNavigation.GoToAbout();
        });
    }

    private void ShowAddOns()
    {
        _telemetryService.ExecuteActivity(ActivityIds.ShowAddOns, async () =>
        {
            _appNavigation.GoToAddOns();
        });
    }

    private void ExitApplication()
    {
        _telemetryService.ExecuteActivity(ActivityIds.ExitApplication, async () =>
        {
            _appController.Shutdown();
        });
    }
}
