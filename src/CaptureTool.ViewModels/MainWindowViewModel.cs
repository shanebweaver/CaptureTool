using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Themes;

namespace CaptureTool.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, INavigationHandler
{
    private readonly IThemeService _themeService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    public event EventHandler<NavigationRequest>? NavigationRequested;
    public event EventHandler<AppWindowPresenterAction>? PresentationUpdateRequested;

    private AppTheme _currentAppTheme;
    public AppTheme CurrentAppTheme
    {
        get => _currentAppTheme;
        set => Set(ref _currentAppTheme, value);
    }

    private AppTheme _defaultAppTheme;
    public AppTheme DefaultAppTheme
    {
        get => _defaultAppTheme;
        set => Set(ref _defaultAppTheme, value);
    }

    public MainWindowViewModel(
        IThemeService themeService,
        IAppController appController,
        ICancellationService cancellationService,
        ISettingsService settingsService,
        INavigationService navigationService)
    {
        _themeService = themeService;
        _appController = appController;
        _cancellationService = cancellationService;
        _navigationService = navigationService;
        _settingsService = settingsService;

        _themeService.CurrentThemeChanged += OnCurrentThemeChanged;
        _appController.AppWindowPresentationUpdateRequested += OnAppWindowPresentationUpdateRequested;
    }

    ~MainWindowViewModel()
    {
        _themeService.CurrentThemeChanged -= OnCurrentThemeChanged;
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

            // App theme
            DefaultAppTheme = _themeService.DefaultTheme;
            CurrentAppTheme = _themeService.CurrentTheme;

            // Go home
            _navigationService.Navigate(CaptureToolNavigationRoutes.Home);
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

    private void OnCurrentThemeChanged(object? sender, AppTheme newTheme)
    {
        CurrentAppTheme = newTheme;
    }

    public override void Unload()
    {
        DefaultAppTheme = 0;
        CurrentAppTheme = 0;
        base.Unload();
    }

    public void HandleNavigationRequest(NavigationRequest request)
    {
        NavigationRequested?.Invoke(this, request);
    }
}
