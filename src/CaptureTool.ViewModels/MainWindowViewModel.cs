using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Core;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;

namespace CaptureTool.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, INavigationHandler
{
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    public event EventHandler<NavigationRequest>? NavigationRequested;
    public event EventHandler<AppWindowPresenterAction>? PresentationUpdateRequested;

    public MainWindowViewModel(
        IAppController appController,
        ICancellationService cancellationService,
        ISettingsService settingsService,
        INavigationService navigationService)
    {
        _appController = appController;
        _cancellationService = cancellationService;
        _navigationService = navigationService;
        _settingsService = settingsService;

        _appController.AppWindowPresentationUpdateRequested += OnAppWindowPresentationUpdateRequested;
    }

    ~MainWindowViewModel()
    {
        _appController.AppWindowPresentationUpdateRequested -= OnAppWindowPresentationUpdateRequested;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            // Navigation handler
            _navigationService.SetNavigationHandler(this);
            cts.Token.ThrowIfCancellationRequested();
            // Settings service
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsFilePath = Path.Combine(appDataPath, "CaptureTool", "Settings.json");
            await _settingsService.InitializeAsync(settingsFilePath, cts.Token);
            cts.Token.ThrowIfCancellationRequested();

            // Go home
            _navigationService.Navigate(NavigationRoutes.Home);
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

    private void OnAppWindowPresentationUpdateRequested(object? sender, AppWindowPresenterAction e)
    {
        PresentationUpdateRequested?.Invoke(this, e);
    }

    public override void Unload()
    {
        base.Unload();
    }

    public void HandleNavigationRequest(NavigationRequest request)
    {
        NavigationRequested?.Invoke(this, request);
    }
}
