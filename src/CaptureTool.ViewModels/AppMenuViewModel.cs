using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Telemetry;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : ViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadAppMenu";
        public static readonly string NewImageCapture = "NewImageCapture";
        public static readonly string OpenFile = "OpenFile";
        public static readonly string NavigateToSettings = "NavigateToSettings";
        public static readonly string ShowAboutApp = "ShowAboutApp";
        public static readonly string ShowAddOns = "ShowAddOns";
        public static readonly string ExitApplication = "ExitApplication";
        public static readonly string SendFeedback = "SendFeedback";
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
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.NewImageCapture, () =>
        {
            _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
        });
    }

    private async void OpenFile()
    {
        await TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.OpenFile, async () =>
        {
            nint hwnd = _appController.GetMainWindowHandle();
            IFile imageFile = await _filePickerService.PickFileAsync(hwnd, FileType.Image, UserFolder.Pictures) 
                ?? throw new OperationCanceledException("No file was selected.");
            _appNavigation.GoToImageEdit(new(imageFile.FilePath));
        });
    }

    private void NavigateToSettings()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.NavigateToSettings, () =>
        {
            _appNavigation.GoToSettings();
        });
    }

    private void ShowAboutApp()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ShowAboutApp, () =>
        {
            _appNavigation.GoToAbout();
        });
    }

    private void ShowAddOns()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ShowAddOns, () =>
        {
            _appNavigation.GoToAddOns();
        });
    }

    private void ExitApplication()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ExitApplication, () =>
        {
            _appController.Shutdown();
        });
    }
}
