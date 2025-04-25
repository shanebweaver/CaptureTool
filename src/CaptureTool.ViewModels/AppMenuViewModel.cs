using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Core;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.ViewModels.Commands;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : ViewModelBase
{
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;
    private readonly IFeatureManager _featureManager;

    public RelayCommand NewDesktopCaptureCommand => new(NewDesktopCapture, () => IsDesktopCaptureEnabled);
    public RelayCommand NewAudioCaptureCommand => new(NewAudioCapture, () => IsAudioCaptureEnabled);
    public RelayCommand NewCameraCaptureCommand => new(NewCameraCapture, () => IsCameraCaptureEnabled);
    public RelayCommand OpenFileCommand => new(OpenFile);
    public RelayCommand GoToSettingsCommand => new(GoToSettings);
    public RelayCommand GoToAboutCommand => new(GoToAbout);
    public RelayCommand ExitApplicationCommand => new(ExitApplication);

    private bool _isDesktopCaptureEnabled;
    public bool IsDesktopCaptureEnabled
    {
        get => _isDesktopCaptureEnabled;
        set => Set(ref _isDesktopCaptureEnabled, value);
    }

    private bool _isAudioCaptureEnabled;
    public bool IsAudioCaptureEnabled
    {
        get => _isAudioCaptureEnabled;
        set => Set(ref _isAudioCaptureEnabled, value);
    }

    private bool _isCameraCaptureEnabled;
    public bool IsCameraCaptureEnabled
    {
        get => _isCameraCaptureEnabled;
        set => Set(ref _isCameraCaptureEnabled, value);
    }

    public AppMenuViewModel(
        ICancellationService cancellationService,
        IAppController appController,
        INavigationService navigationService,
        IFeatureManager featureManager)
    {
        _cancellationService = cancellationService;
        _appController = appController;
        _navigationService = navigationService;
        _featureManager = featureManager;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            IsDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture);
            IsAudioCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_AudioCapture);
            IsCameraCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_CameraCapture);
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

    override public void Unload()
    {
        base.Unload();
    }

    private void NewDesktopCapture()
    {
        DesktopCaptureOptions options = new(DesktopImageCaptureMode.Rectangle, ImageFileType.Png, true);
        _ = _appController.NewDesktopCaptureAsync(options);
    }

    private void NewCameraCapture()
    {
        _ = _appController.NewCameraCaptureAsync();
    }

    private void NewAudioCapture()
    {
        _ = _appController.NewAudioCaptureAsync();
    }

    private async void OpenFile()
    {
        var filePicker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        filePicker.FileTypeFilter.Add(".png");

        nint hwnd = _appController.GetMainWindowHandle();
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        StorageFile file = await filePicker.PickSingleFileAsync();
        if (file != null)
        {
            ImageFile imageFile = new(file.Path);
            _navigationService.Navigate(NavigationRoutes.ImageEdit, imageFile);
        }
    }

    private void GoToSettings()
    {
        _navigationService.Navigate(NavigationRoutes.Settings);
    }

    private void GoToAbout()
    {
        _navigationService.Navigate(NavigationRoutes.About);
    }

    private void ExitApplication()
    {
        _appController.Shutdown();
    }
}
