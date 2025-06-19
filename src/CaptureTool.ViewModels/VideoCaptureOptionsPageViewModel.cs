using CaptureTool.Capture.Video;
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

public sealed partial class VideoCaptureOptionsPageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "VideoCaptureOptionsPageViewModel_Load";
        public static readonly string Unload = "VideoCaptureOptionsPageViewModel_Unload";
        public static readonly string NewVideoCapture = "VideoCaptureOptionsPageViewModel_NewVideoCapture";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ISettingsService _settingsService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;

    public RelayCommand NewVideoCaptureCommand => new(NewVideoCapture, () => IsVideoCaptureEnabled);

    private ObservableCollection<VideoCaptureMode> _captureModes;
    public ObservableCollection<VideoCaptureMode> CaptureModes
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

    private bool _isVideoCaptureEnabled;
    public bool IsVideoCaptureEnabled
    {
        get => _isVideoCaptureEnabled;
        set => Set(ref _isVideoCaptureEnabled, value);
    }

    private bool _autoSave;
    public bool AutoSave
    {
        get => _autoSave;
        set => Set(ref _autoSave, value);
    }

    public VideoCaptureOptionsPageViewModel(
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
            IsVideoCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture_Video);
            VideoCaptureMode[] supportedModes = Enum.GetValues<VideoCaptureMode>();
            foreach (var captureMode in supportedModes)
            {
                CaptureModes.Add(captureMode);
            }

            SelectedCaptureModeIndex = 0;
            AutoSave = _settingsService.Get(CaptureToolSettings.VideoCapture_Options_AutoSave);

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
            IsVideoCaptureEnabled = false;
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

    private async void NewVideoCapture()
    {
        string activityId = ActivityIds.NewVideoCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            var videoCaptureMode = CaptureModes[SelectedCaptureModeIndex];
            VideoCaptureOptions options = new(videoCaptureMode, VideoFileType.Mp4, _autoSave);
            await _appController.NewVideoCaptureAsync(options);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
