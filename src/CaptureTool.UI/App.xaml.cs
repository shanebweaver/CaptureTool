using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Cancellation;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.UI;

public partial class App : Application
{
    public new static App Current => (App)Application.Current;
 
    public Ioc Ioc => _ioc;

    private readonly Ioc _ioc;
    private readonly SemaphoreSlim _shutdownSemaphore;
    private bool _isShuttingDown;
    private Window? _window;

    public App()
    {
        _ioc = new();
        _shutdownSemaphore = new(1, 1);
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs launchArgs)
    {
        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        _ = ActivateAsync(args, CancellationToken.None);
    }

    public async Task ActivateAsync(AppActivationArguments args, CancellationToken cancellationToken)
    {
        try
        {
            await _ioc.InitializeServicesAsync(cancellationToken);

            switch (args.Kind)
            {
                case ExtendedActivationKind.Launch:
                    HandleLaunchActivation(args);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected activation kind");
            }
        }
        catch (Exception e)
        {
            ServiceLocator.Logging.LogException(e, "Activation failed.");
            CheckExit();
        }
    }

    private void HandleLaunchActivation(AppActivationArguments args)
    {
        if (_window == null)
        {
            _window = new MainWindow();
            _window.Closed += OnWindowClosed;
        }
        _window.Activate();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _window = null;
        CheckExit();
    }

    private void CheckExit()
    {
        if (_window == null)
        {
            _ = ShutdownAsync();
        }
    }

    private async Task ShutdownAsync()
    {
        await _shutdownSemaphore.WaitAsync();
        try
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;

            // TODO: Show teardown UI

            await _ioc.GetService<ICancellationService>().CancelAllAsync();
            await _ioc.DisposeAsync();

            _window?.Close();
        }
        catch (Exception e)
        {
            // Error during shutdown.
            Debug.Fail($"Error during shutdown: {e.Message}");
        }
        finally 
        {
            _shutdownSemaphore.Release();
        }

        _shutdownSemaphore.Dispose();
        Application.Current.Exit();
    }
}
