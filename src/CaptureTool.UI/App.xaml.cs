using System;
using System.Diagnostics;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Themes;
using CaptureTool.UI.Activation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.UI;

public partial class App : Application
{
    internal new static App Current => (App)Application.Current;

    internal CaptureToolServiceProvider ServiceProvider { get; }
    internal DispatcherQueue DispatcherQueue { get; }
    internal MainWindow? MainWindow { get; private set; }

    public App()
    {
        ServiceProvider = new();
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();
        InitializeComponent();
        RestoreAppTheme();
    }

    private void RestoreAppTheme()
    {
        IThemeService themeService = ServiceProvider.GetService<IThemeService>();
        
        AppTheme defaultTheme = RequestedTheme == ApplicationTheme.Light ? AppTheme.Light : AppTheme.Dark;
        themeService.Initialize(defaultTheme);

        ApplicationTheme? applicationTheme = themeService.CurrentTheme switch
        {
            AppTheme.Dark => ApplicationTheme.Dark,
            AppTheme.Light => ApplicationTheme.Light,
            _ => null
        };

        if (applicationTheme != null)
        {
            RequestedTheme = applicationTheme.Value;
        }
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
                case ExtendedActivationKind.Protocol:
                    ProtocolActivationHandler.HandleActivation(args);
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
        if (MainWindow == null)
        {
            MainWindow = new MainWindow();
            MainWindow.Closed += OnWindowClosed;
        }
        MainWindow.Activate();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        CleanupWindow();
        CheckExit();
    }

    private void CheckExit()
    {
        if (MainWindow == null)
        {
            Shutdown();
        }
    }

    private void CleanupWindow()
    {
        if (MainWindow != null)
        {
            MainWindow.Closed -= OnWindowClosed;
            MainWindow.Close();
            MainWindow = null;
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
