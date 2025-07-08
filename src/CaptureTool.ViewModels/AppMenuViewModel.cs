using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Feedback;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Telemetry;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "AppMenuViewModel_Load";
        public static readonly string Unload = "AppMenuViewModel_Unload";
        public static readonly string NewImageCapture = "AppMenuViewModel_NewImageCapture";
        public static readonly string OpenFile = "AppMenuViewModel_OpenFile";
        public static readonly string NavigateToSettings = "AppMenuViewModel_NavigateToSettings";
        public static readonly string ShowAboutApp = "AppMenuViewModel_ShowAboutApp";
        public static readonly string ExitApplication = "AppMenuViewModel_ExitApplication";
        public static readonly string SendFeedback = "AppMenuViewModel_SendFeedback";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;
    private readonly IFeedbackService _feedbackService;
    private readonly IFilePickerService _filePickerService;

    public RelayCommand NewImageCaptureCommand => new(NewImageCapture);
    public RelayCommand OpenFileCommand => new(OpenFile);
    public RelayCommand NavigateToSettingsCommand => new(NavigateToSettings);
    public RelayCommand ShowAboutAppCommand => new(ShowAboutApp);
    public RelayCommand SendFeedbackCommand => new(SendFeedback);
    public RelayCommand ExitApplicationCommand => new(ExitApplication);

    private bool _showSendFeedbackOption;
    public bool ShowSendFeedbackOption
    {
        get => _showSendFeedbackOption;
        set => Set(ref _showSendFeedbackOption, value);
    }

    public bool IsSendFeedbackEnabled { get; }

    public AppMenuViewModel(
        ITelemetryService telemetryService,
        IAppController appController,
        IFeedbackService feedbackService,
        INavigationService navigationService,
        IFilePickerService filePickerService,
        IFeatureManager featureManager)
    {
        _telemetryService = telemetryService;
        _appController = appController;
        _feedbackService = feedbackService;
        _navigationService = navigationService;
        _filePickerService = filePickerService;

        IsSendFeedbackEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_UserFeedback);
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        string activityId = ActivityIds.Load;
        _telemetryService.ActivityInitiated(activityId);
        try
        {
            if (IsSendFeedbackEnabled)
            {
                ShowSendFeedbackOption = await _feedbackService.IsFeedbackSupportedAsync();
            }

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
            throw;
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        ShowSendFeedbackOption = false;
        base.Unload();
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

            _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);

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

    private void SendFeedback()
    {
        string activityId = ActivityIds.SendFeedback;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _feedbackService.ShowFeedbackUIAsync();
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
