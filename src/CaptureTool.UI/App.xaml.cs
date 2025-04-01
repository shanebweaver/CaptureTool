using System;
using System.Diagnostics;
using CaptureTool.Services.Cancellation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.UI;

public partial class App : Application
{
    internal new static App Current => (App)Application.Current;

    internal CaptureToolServiceProvider ServiceProvider { get; }

    private Window? _window;

    public App()
    {
        ServiceProvider = new();
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs launchArgs)
    {
        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        Activate(args);
    }

    internal void Activate(AppActivationArguments args)
    {
        try
        {
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
        CleanupWindow();
        CheckExit();
    }

    private void CheckExit()
    {
        if (_window == null)
        {
            Shutdown();
        }
    }

    private void CleanupWindow()
    {
        if (_window != null)
        {
            _window.Closed -= OnWindowClosed;
            _window.Close();
            _window = null;
        }
    }

    internal void Shutdown()
    {
        lock (this)
        {
            try
            {
                CleanupWindow();

                // Cancel all running tasks
                ServiceProvider.GetRequiredService<ICancellationService>().CancelAll();

                // Dispose all services
                ServiceProvider.Dispose();
            }
            catch (Exception e)
            {
                // Error during shutdown.
                Debug.Fail($"Error during shutdown: {e.Message}");
            }

            Application.Current.Exit();
        }
    }
}
