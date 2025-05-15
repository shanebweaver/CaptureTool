using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Capture.Desktop.SnippingTool;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.ViewModels.Commands;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;
    private readonly INavigationService _navigationService;

    public RelayCommand NewDesktopImageCaptureCommand => new(NewDesktopImageCapture, () => IsDesktopImageCaptureEnabled);
    public RelayCommand NewDesktopVideoCaptureCommand => new(NewDesktopVideoCapture, () => IsDesktopVideoCaptureEnabled);
    public RelayCommand NewDesktopAudioCaptureCommand => new(NewDesktopAudioCapture, () => IsDesktopAudioCaptureEnabled);
    public RelayCommand DesktopImageCaptureOptionsCommand => new(DesktopImageCaptureOptions, () => IsDesktopImageCaptureOptionsEnabled);
    public RelayCommand DesktopVideoCaptureOptionsCommand => new(DesktopVideoCaptureOptions, () => IsDesktopVideoCaptureOptionsEnabled);
    public RelayCommand DesktopAudioCaptureOptionsCommand => new(DesktopAudioCaptureOptions, () => IsDesktopAudioCaptureOptionsEnabled);

    private bool _isDesktopImageCaptureEnabled;
    public bool IsDesktopImageCaptureEnabled
    {
        get => _isDesktopImageCaptureEnabled;
        set => Set(ref _isDesktopImageCaptureEnabled, value);
    }

    private bool _isDesktopImageCaptureOptionsEnabled;
    public bool IsDesktopImageCaptureOptionsEnabled
    {
        get => _isDesktopImageCaptureOptionsEnabled;
        set => Set(ref _isDesktopImageCaptureOptionsEnabled, value);
    }

    private bool _isDesktopVideoCaptureEnabled;
    public bool IsDesktopVideoCaptureEnabled
    {
        get => _isDesktopVideoCaptureEnabled;
        set => Set(ref _isDesktopVideoCaptureEnabled, value);
    }

    private bool _isDesktopVideoCaptureOptionsEnabled;
    public bool IsDesktopVideoCaptureOptionsEnabled
    {
        get => _isDesktopVideoCaptureOptionsEnabled;
        set => Set(ref _isDesktopVideoCaptureOptionsEnabled, value);
    }

    private bool _isDesktopAudioCaptureEnabled;
    public bool IsDesktopAudioCaptureEnabled
    {
        get => _isDesktopAudioCaptureEnabled;
        set => Set(ref _isDesktopAudioCaptureEnabled, value);
    }

    private bool _isDesktopAudioCaptureOptionsEnabled;
    public bool IsDesktopAudioCaptureOptionsEnabled
    {
        get => _isDesktopAudioCaptureOptionsEnabled;
        set => Set(ref _isDesktopAudioCaptureOptionsEnabled, value);
    }

    public HomePageViewModel(
        IAppController appController,
        ICancellationService cancellationService,
        IFeatureManager featureManager,
        INavigationService navigationService)
    {
        _appController = appController;
        _cancellationService = cancellationService;
        _featureManager = featureManager;
        _navigationService = navigationService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            // Desktop Image
            IsDesktopImageCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image);
            IsDesktopImageCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image_Options);

            // Desktop Video
            IsDesktopVideoCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video);
            IsDesktopVideoCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video_Options);

            // Desktop Audio
            IsDesktopAudioCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Audio);
            IsDesktopAudioCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Audio_Options);
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
        _isDesktopImageCaptureEnabled = false;
        _isDesktopVideoCaptureEnabled = false;
        _isDesktopAudioCaptureEnabled = false;

        base.Unload();
    }

    private void NewDesktopImageCapture()
    {
        // TODO: Remember last used options
        DesktopImageCaptureOptions options = new(DesktopImageCaptureMode.Rectangle, ImageFileType.Png, true);
        _ = _appController.NewDesktopImageCaptureAsync(options);
    }

    private void NewDesktopVideoCapture()
    {
        // TODO: Remember last used options
        DesktopVideoCaptureOptions options = new(DesktopVideoCaptureMode.Rectangle, VideoFileType.Mp4, true);
        _ = _appController.NewDesktopVideoCaptureAsync(options);
    }

    private void NewDesktopAudioCapture()
    {
        _ = _appController.NewDesktopAudioCaptureAsync();
    }

    private void DesktopImageCaptureOptions()
    {
        _navigationService.Navigate(CaptureToolNavigationRoutes.DesktopImageCaptureOptions, null);
    }

    private void DesktopVideoCaptureOptions()
    {
        _navigationService.Navigate(CaptureToolNavigationRoutes.DesktopVideoCaptureOptions, null);
    }

    private void DesktopAudioCaptureOptions()
    {
        throw new NotImplementedException();
    }
}
