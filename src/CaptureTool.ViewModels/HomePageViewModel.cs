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

    public ICommand NewDesktopCaptureCommand => new RelayCommand(NewDesktopCapture);
    public ICommand NewVideoCaptureCommand => new RelayCommand(NewVideoCapture);
    public ICommand NewAudioCaptureCommand => new RelayCommand(NewAudioCapture);

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
            // Load here
            _snippingToolService.ResponseReceived += OnSnippingToolResponseReceived;
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
        var file = await e.GetFileAsync();
        _navigationService.Navigate(NavigationRoutes.ImageCaptureResults, file);
    }

    public override void Unload()
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
}
