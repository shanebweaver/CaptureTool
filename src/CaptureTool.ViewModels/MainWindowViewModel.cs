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
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    public event Action<NavigationRequest>? NavigationRequested;

    public MainWindowViewModel(
        ICancellationService cancellationService,
        ISettingsService settingsService,
        INavigationService navigationService)
    {
        _cancellationService = cancellationService;
        _navigationService = navigationService;
        _settingsService = settingsService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);

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
            _navigationService.Navigate(NavigationKeys.Home);
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
        base.Unload();
    }

    public void HandleNavigationRequest(NavigationRequest request)
    {
        NavigationRequested?.Invoke(request);
    }
}
