using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Core;
using CaptureTool.FeatureManagement;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.ViewModels.Commands;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopCaptureOptionsPageViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;
    private readonly IFactoryService<DesktopCaptureModeViewModel> _desktopCaptureModeViewModelfactory;

    public RelayCommand NewDesktopCaptureCommand => new(NewDesktopCapture, () => IsDesktopCaptureEnabled);
    
    private bool _isDesktopCaptureEnabled;
    public bool IsDesktopCaptureEnabled
    {
        get => _isDesktopCaptureEnabled;
        set => Set(ref _isDesktopCaptureEnabled, value);
    }

    private ObservableCollection<DesktopCaptureModeViewModel> _desktopCaptureModes;
    public ObservableCollection<DesktopCaptureModeViewModel> DesktopCaptureModes
    {
        get => _desktopCaptureModes;
        set => Set(ref _desktopCaptureModes, value);
    }

    private int _selectedDesktopCaptureModeIndex;
    public int SelectedDesktopCaptureModeIndex
    {
        get => _selectedDesktopCaptureModeIndex;
        set => Set(ref _selectedDesktopCaptureModeIndex, value);
    }

    private bool _isImageDesktopCaptureEnabled;
    public bool IsImageDesktopCaptureEnabled
    {
        get => _isImageDesktopCaptureEnabled;
        set => Set(ref _isImageDesktopCaptureEnabled, value);
    }

    private bool _isVideoDesktopCaptureEnabled;
    public bool IsVideoDesktopCaptureEnabled
    {
        get => _isVideoDesktopCaptureEnabled;
        set => Set(ref _isVideoDesktopCaptureEnabled, value);
    }

    private ObservableCollection<DesktopImageCaptureMode> _imageCaptureModes;
    public ObservableCollection<DesktopImageCaptureMode> ImageCaptureModes
    {
        get => _imageCaptureModes;
        set => Set(ref _imageCaptureModes, value);
    }

    private ObservableCollection<DesktopVideoCaptureMode> _videoCaptureModes;
    public ObservableCollection<DesktopVideoCaptureMode> VideoCaptureModes
    {
        get => _videoCaptureModes;
        set => Set(ref _videoCaptureModes, value);
    }

    private int _selectedImageCaptureModeIndex;
    public int SelectedImageCaptureModeIndex
    {
        get => _selectedImageCaptureModeIndex;
        set => Set(ref _selectedImageCaptureModeIndex, value);
    }

    private int _selectedVideoCaptureModeIndex;
    public int SelectedVideoCaptureModeIndex
    {
        get => _selectedVideoCaptureModeIndex;
        set => Set(ref _selectedVideoCaptureModeIndex, value);
    }

    private bool _autoSave;
    public bool AutoSave
    {
        get => _autoSave;
        set => Set(ref _autoSave, value);
    }

    public DesktopCaptureOptionsPageViewModel(
        IAppController appController,
        ICancellationService cancellationService,
        IFeatureManager featureManager,
        IFactoryService<DesktopCaptureModeViewModel> desktopCaptureModeViewModelfactory)
    {
        _appController = appController;
        _cancellationService = cancellationService;
        _featureManager = featureManager;
        _desktopCaptureModeViewModelfactory = desktopCaptureModeViewModelfactory;

        _desktopCaptureModes = [];
        _imageCaptureModes = [];
        _videoCaptureModes = [];
        _selectedImageCaptureModeIndex = -1;
        _selectedVideoCaptureModeIndex = -1;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            IsImageDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image);
            if (IsImageDesktopCaptureEnabled)
            {
                DesktopImageCaptureMode[] supportedModes = Enum.GetValues<DesktopImageCaptureMode>();
                if (supportedModes.Length > 0)
                {
                    foreach (var captureMode in supportedModes)
                    {
                        ImageCaptureModes.Add(captureMode);
                    }

                    SelectedImageCaptureModeIndex = 0;
                }
            }

            IsVideoDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video);
            if (IsVideoDesktopCaptureEnabled)
            {
                DesktopVideoCaptureMode[] supportedModes = Enum.GetValues<DesktopVideoCaptureMode>();
                if (supportedModes.Length > 0)
                {
                    foreach (var captureMode in supportedModes)
                    {
                        VideoCaptureModes.Add(captureMode);
                    }

                    SelectedVideoCaptureModeIndex = 0;
                }
            }

            IsDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture);
            if (IsDesktopCaptureEnabled)
            {
                if (IsImageDesktopCaptureEnabled)
                {
                    DesktopCaptureModeViewModel vm = _desktopCaptureModeViewModelfactory.Create();
                    DesktopCaptureModes.Add(vm);
                    await vm.LoadAsync(DesktopCaptureMode.Image, cts.Token);
                }

                if (IsVideoDesktopCaptureEnabled)
                {
                    DesktopCaptureModeViewModel vm = _desktopCaptureModeViewModelfactory.Create();
                    DesktopCaptureModes.Add(vm);
                    await vm.LoadAsync(DesktopCaptureMode.Video, cts.Token);
                }

                SelectedDesktopCaptureModeIndex = 0;
            }
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
        IsDesktopCaptureEnabled = false;
        DesktopCaptureModes.Clear();
        SelectedDesktopCaptureModeIndex = 0;

        IsImageDesktopCaptureEnabled = false;
        ImageCaptureModes.Clear();
        SelectedImageCaptureModeIndex = 0;

        IsVideoDesktopCaptureEnabled = false;
        VideoCaptureModes.Clear();
        SelectedVideoCaptureModeIndex = 0;

        base.Unload();
    }

    private void NewDesktopCapture()
    {
        var imageCaptureMode = ImageCaptureModes[SelectedImageCaptureModeIndex];
        DesktopCaptureOptions options = new(imageCaptureMode, ImageFileType.Png, _autoSave);
        _ = _appController.NewDesktopCaptureAsync(options);
    }
}
