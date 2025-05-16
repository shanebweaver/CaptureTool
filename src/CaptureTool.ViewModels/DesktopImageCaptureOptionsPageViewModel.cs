using CaptureTool.Capture.Desktop;
using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Telemetry;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopImageCaptureOptionsPageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "DesktopImageCaptureOptionsPageViewModel_Load";
        public static readonly string Unload = "DesktopImageCaptureOptionsPageViewModel_Unload";
        public static readonly string NewDesktopImageCapture = "DesktopImageCaptureOptionsPageViewModel_NewDesktopImageCapture";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ISettingsService _settingsService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;

    public RelayCommand NewDesktopImageCaptureCommand => new(NewDesktopImageCapture, () => IsImageDesktopCaptureEnabled);

    private ObservableCollection<DesktopImageCaptureMode> _captureModes;
    public ObservableCollection<DesktopImageCaptureMode> CaptureModes
    {
        get => _captureModes;
        set => Set(ref _captureModes, value);
    }

    private int _selectedImageCaptureModeIndex;
    public int SelectedImageCaptureModeIndex
    {
        get => _selectedImageCaptureModeIndex;
        set => Set(ref _selectedImageCaptureModeIndex, value);
    }

    private bool _isImageDesktopCaptureEnabled;
    public bool IsImageDesktopCaptureEnabled
    {
        get => _isImageDesktopCaptureEnabled;
        set => Set(ref _isImageDesktopCaptureEnabled, value);
    }

    private bool _autoSave;
    public bool AutoSave
    {
        get => _autoSave;
        set => Set(ref _autoSave, value);
    }

    public DesktopImageCaptureOptionsPageViewModel(
        ITelemetryService telemetryService,
        ISettingsService settingsService,
        IAppController appController,
        ICancellationService cancellationService,
        IFeatureManager featureManager)
    {
        _telemetryService = telemetryService;
        _settingsService = settingsService;
        _appController = appController;
        _cancellationService = cancellationService;
        _featureManager = featureManager;

        _captureModes = [];
        _selectedImageCaptureModeIndex = -1;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        string activityId = ActivityIds.Load;
        _telemetryService.ActivityInitiated(activityId);

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            IsImageDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image);
            if (IsImageDesktopCaptureEnabled)
            {
                DesktopImageCaptureMode[] supportedModes = Enum.GetValues<DesktopImageCaptureMode>();
                foreach (var captureMode in supportedModes)
                {
                    CaptureModes.Add(captureMode);
                }

                SelectedImageCaptureModeIndex = 0;
            }

            AutoSave = _settingsService.Get(CaptureToolSettings.DesktopImageCapture_Options_AutoSave);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            _telemetryService.ActivityCanceled(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        string activityId = ActivityIds.Unload;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            IsImageDesktopCaptureEnabled = false;
            CaptureModes.Clear();
            SelectedImageCaptureModeIndex = -1;
            AutoSave = false;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private async void NewDesktopImageCapture()
    {
        string activityId = ActivityIds.NewDesktopImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            var imageCaptureMode = CaptureModes[SelectedImageCaptureModeIndex];
            DesktopImageCaptureOptions options = new(imageCaptureMode, ImageFileType.Png, _autoSave);
            await _appController.NewDesktopImageCaptureAsync(options);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }
}
