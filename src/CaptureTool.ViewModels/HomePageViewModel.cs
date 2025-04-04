using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CaptureTool.Core;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.AppController;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.SnippingTool;
using CaptureTool.ViewModels.Commands;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;
    private readonly ILocalizationService _localizationService;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly ISnippingToolService _snippingToolService;

    public ICommand NewDesktopCaptureCommand => new RelayCommand(NewDesktopCapture, () => IsDesktopCaptureEnabled);
    public ICommand NewAudioCaptureCommand => new RelayCommand(NewAudioCapture, () => IsAudioCaptureEnabled);
    public ICommand NewCameraCaptureCommand => new RelayCommand(NewCameraCapture, () => IsCameraCaptureEnabled);
    public ICommand DesktopCaptureOptionsCommand => new RelayCommand(DesktopCaptureOptions, () => IsDesktopCaptureOptionsEnabled);
    public ICommand AudioCaptureOptionsCommand => new RelayCommand(AudioCaptureOptions, () => IsAudioCaptureOptionsEnabled);
    public ICommand CameraCaptureOptionsCommand => new RelayCommand(CameraCaptureOptions, () => IsCameraCaptureOptionsEnabled);

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

    private bool _isDesktopCaptureOptionsEnabled;
    public bool IsDesktopCaptureOptionsEnabled
    {
        get => _isDesktopCaptureOptionsEnabled;
        set => Set(ref _isDesktopCaptureOptionsEnabled, value);
    }

    private bool _isAudioCaptureOptionsEnabled;
    public bool IsAudioCaptureOptionsEnabled
    {
        get => _isAudioCaptureOptionsEnabled;
        set => Set(ref _isAudioCaptureOptionsEnabled, value);
    }

    private bool _isCameraCaptureOptionsEnabled;
    public bool IsCameraCaptureOptionsEnabled
    {
        get => _isCameraCaptureOptionsEnabled;
        set => Set(ref _isCameraCaptureOptionsEnabled, value);
    }

    public HomePageViewModel(
        IAppController appController,
        ICancellationService cancellationService,
        IFeatureManager featureManager,
        ILocalizationService localizationService,
        INavigationService navigationService,
        ISettingsService settingsService,
        ISnippingToolService snippingToolService)
    {
        _appController = appController;
        _cancellationService = cancellationService;
        _featureManager = featureManager;
        _localizationService = localizationService;
        _navigationService = navigationService;
        _settingsService = settingsService;
        _snippingToolService = snippingToolService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            _snippingToolService.ResponseReceived += OnSnippingToolResponseReceived;

            IsDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture);
            IsAudioCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_AudioCapture);
            IsCameraCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_CameraCapture);
            IsDesktopCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCaptureOptions);
            IsAudioCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_AudioCaptureOptions);
            IsCameraCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_CameraCaptureOptions);
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

    private async void OnSnippingToolResponseReceived(object? sender, SnippingToolResponse e)
    {
        _appController.UpdateAppWindowPresentation(AppWindowPresenterAction.Restore);

        if (e.Code == 200)
        {
            var file = await e.GetFileAsync();
            _navigationService.Navigate(NavigationRoutes.ImageEdit, file);
        }
        else
        {
            _navigationService.ClearNavigationHistory();
            _navigationService.Navigate(NavigationRoutes.Home, null); // TODO: Do something with the error.
        }
    }

    public override void Unload()
    {
        _snippingToolService.ResponseReceived -= OnSnippingToolResponseReceived;

        _isDesktopCaptureEnabled = false;
        _isAudioCaptureEnabled = false;
        _isCameraCaptureEnabled = false;

        base.Unload();
    }

    private void NewDesktopCapture()
    {
        _appController.NewDesktopCapture();
    }

    private void NewCameraCapture()
    {
        _appController.NewCameraCapture();
    }

    private void NewAudioCapture()
    {
        _appController.NewAudioCapture();
    }

    private void DesktopCaptureOptions()
    {
        _navigationService.Navigate(NavigationRoutes.DesktopCaptureOptions, null);
    }

    private void AudioCaptureOptions()
    {
        throw new NotImplementedException();
    }

    private void CameraCaptureOptions()
    {
        throw new NotImplementedException();
    }
}
