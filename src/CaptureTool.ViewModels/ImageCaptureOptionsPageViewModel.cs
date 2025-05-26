using CaptureTool.Capture.Image;
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

public sealed partial class ImageCaptureOptionsPageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "ImageCaptureOptionsPageViewModel_Load";
        public static readonly string Unload = "ImageCaptureOptionsPageViewModel_Unload";
        public static readonly string NewImageCapture = "ImageCaptureOptionsPageViewModel_NewImageCapture";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ISettingsService _settingsService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;

    public RelayCommand NewImageCaptureCommand => new(NewImageCapture, () => IsImageCaptureEnabled);

    private ObservableCollection<ImageCaptureMode> _captureModes;
    public ObservableCollection<ImageCaptureMode> CaptureModes
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

    private bool _isImageCaptureEnabled;
    public bool IsImageCaptureEnabled
    {
        get => _isImageCaptureEnabled;
        set => Set(ref _isImageCaptureEnabled, value);
    }

    private bool _autoSave;
    public bool AutoSave
    {
        get => _autoSave;
        set => Set(ref _autoSave, value);
    }

    public ImageCaptureOptionsPageViewModel(
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
            IsImageCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture_Image);
            if (IsImageCaptureEnabled)
            {
                ImageCaptureMode[] supportedModes = Enum.GetValues<ImageCaptureMode>();
                foreach (var captureMode in supportedModes)
                {
                    CaptureModes.Add(captureMode);
                }

                SelectedImageCaptureModeIndex = 0;
            }

            AutoSave = _settingsService.Get(CaptureToolSettings.ImageCapture_Options_AutoSave);

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
            IsImageCaptureEnabled = false;
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

    private async void NewImageCapture()
    {
        string activityId = ActivityIds.NewImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            var imageCaptureMode = CaptureModes[SelectedImageCaptureModeIndex];
            ImageCaptureOptions options = new(imageCaptureMode, ImageFileType.Png, _autoSave);
            await _appController.NewImageCaptureAsync(options);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }
}
