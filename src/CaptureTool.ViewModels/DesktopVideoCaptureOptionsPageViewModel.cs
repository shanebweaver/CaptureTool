using CaptureTool.Capture.Desktop;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Settings;
using CaptureTool.ViewModels.Commands;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopVideoCaptureOptionsPageViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;

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

    public RelayCommand NewDesktopVideoCaptureCommand => new(NewDesktopVideoCapture, () => IsVideoDesktopCaptureEnabled);

    public DesktopVideoCaptureOptionsPageViewModel(
        ISettingsService settingsService,
        IAppController appController,
        ICancellationService cancellationService,
        IFeatureManager featureManager)
    {
        _settingsService = settingsService;
        _appController = appController;
        _cancellationService = cancellationService;
        _featureManager = featureManager;

        _captureModes = [];
        _selectedCaptureModeIndex = -1;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        StartLoading();

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
        }
        catch (OperationCanceledException)
        {
            // Load canceled
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        IsVideoDesktopCaptureEnabled = false;
        CaptureModes.Clear();
        SelectedCaptureModeIndex = 0;
        AutoSave = false;
        base.Unload();
    }

    private void NewDesktopVideoCapture()
    {
        var videoCaptureMode = CaptureModes[SelectedCaptureModeIndex];
        DesktopVideoCaptureOptions options = new(videoCaptureMode, VideoFileType.Mp4, _autoSave);
        _ = _appController.NewDesktopVideoCaptureAsync(options);
    }
}
