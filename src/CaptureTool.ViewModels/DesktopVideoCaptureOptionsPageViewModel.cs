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

public sealed partial class DesktopVideoCaptureOptionsPageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "DesktopVideoCaptureOptionsPageViewModel_Load";
        public static readonly string Unload = "DesktopVideoCaptureOptionsPageViewModel_Unload";
        public static readonly string NewDesktopVideoCapture = "DesktopVideoCaptureOptionsPageViewModel_NewDesktopVideoCapture";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ISettingsService _settingsService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;

    public RelayCommand NewDesktopVideoCaptureCommand => new(NewDesktopVideoCapture, () => IsVideoDesktopCaptureEnabled);

    private ObservableCollection<DesktopVideoCaptureMode> _captureModes;
    public ObservableCollection<DesktopVideoCaptureMode> CaptureModes
    {
        get => _captureModes;
        set => Set(ref _captureModes, value);
    }

    private int _selectedCaptureModeIndex;
    public int SelectedCaptureModeIndex
    {
        get => _selectedCaptureModeIndex;
        set => Set(ref _selectedCaptureModeIndex, value);
    }

    private bool _isVideoDesktopCaptureEnabled;
    public bool IsVideoDesktopCaptureEnabled
    {
        get => _isVideoDesktopCaptureEnabled;
        set => Set(ref _isVideoDesktopCaptureEnabled, value);
    }

    private bool _autoSave;
    public bool AutoSave
    {
        get => _autoSave;
        set => Set(ref _autoSave, value);
    }

    public DesktopVideoCaptureOptionsPageViewModel(
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
        _selectedCaptureModeIndex = -1;
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
            IsVideoDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video);
            DesktopVideoCaptureMode[] supportedModes = Enum.GetValues<DesktopVideoCaptureMode>();
            foreach (var captureMode in supportedModes)
            {
                CaptureModes.Add(captureMode);
            }

            SelectedCaptureModeIndex = 0;
            AutoSave = _settingsService.Get(CaptureToolSettings.DesktopVideoCapture_Options_AutoSave);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            _telemetryService.ActivityCanceled(activityId);
            throw;
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
            throw;
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
            IsVideoDesktopCaptureEnabled = false;
            CaptureModes.Clear();
            SelectedCaptureModeIndex = 0;
            AutoSave = false;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private async void NewDesktopVideoCapture()
    {
        string activityId = ActivityIds.NewDesktopVideoCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            var videoCaptureMode = CaptureModes[SelectedCaptureModeIndex];
            DesktopVideoCaptureOptions options = new(videoCaptureMode, VideoFileType.Mp4, _autoSave);
            await _appController.NewDesktopVideoCaptureAsync(options);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
