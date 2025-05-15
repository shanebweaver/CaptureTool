using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Core;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Settings;
using CaptureTool.ViewModels.Commands;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopImageCaptureOptionsPageViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;

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

    public RelayCommand NewDesktopImageCaptureCommand => new(NewDesktopImageCapture, () => IsImageDesktopCaptureEnabled);

    public DesktopImageCaptureOptionsPageViewModel(
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
        _selectedImageCaptureModeIndex = -1;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        StartLoading();

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
        IsImageDesktopCaptureEnabled = false;
        CaptureModes.Clear();
        SelectedImageCaptureModeIndex = 0;
        AutoSave = false;
        base.Unload();
    }

    private void NewDesktopImageCapture()
    {
        var imageCaptureMode = CaptureModes[SelectedImageCaptureModeIndex];
        DesktopImageCaptureOptions options = new(imageCaptureMode, ImageFileType.Png, _autoSave);
        _ = _appController.NewDesktopImageCaptureAsync(options);
    }
}
