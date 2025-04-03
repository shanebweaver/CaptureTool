using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CaptureTool.Core;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.AppController;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.ViewModels.Commands;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : ViewModelBase
{
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;
    private readonly IFeatureManager _featureManager;

    public ICommand NewDesktopCaptureCommand => new RelayCommand(NewDesktopCapture, () => IsDesktopCaptureEnabled);
    public ICommand NewAudioCaptureCommand => new RelayCommand(NewAudioCapture, () => IsAudioCaptureEnabled);
    public ICommand NewVideoCaptureCommand => new RelayCommand(NewVideoCapture, () => IsVideoCaptureEnabled);
    public ICommand GoToSettingsCommand => new RelayCommand(GoToSettings);
    public ICommand GoToAboutCommand => new RelayCommand(GoToAbout);
    public ICommand ExitApplicationCommand => new RelayCommand(ExitApplication);

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

    private bool _isVideoCaptureEnabled;
    public bool IsVideoCaptureEnabled
    {
        get => _isVideoCaptureEnabled;
        set => Set(ref _isVideoCaptureEnabled, value);
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
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            IsDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture);
            IsAudioCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_AudioCapture);
            IsVideoCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_VideoCapture);
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
        _appController.NewDesktopCapture();
    }

    private void NewVideoCapture()
    {
        _appController.NewVideoCapture();
    }

    private void NewAudioCapture()
    {
        _appController.NewAudioCapture();
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
