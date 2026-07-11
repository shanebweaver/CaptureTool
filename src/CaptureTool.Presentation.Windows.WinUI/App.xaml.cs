using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.Presentation.Windows.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    internal new static App Current => (App)Microsoft.UI.Xaml.Application.Current;

    internal AppServiceProvider ServiceProvider { get; }
    internal DispatcherQueue DispatcherQueue { get; }

    public App()
    {
        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();
        ServiceProvider = new();
        InitializeComponent();
        RestoreAppTheme();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        TrackException(e.Exception, "App", fatal: true);
        ServiceProvider.GetService<ILogService>().LogException(e.Exception, "Unhandled exception occurred.");
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            TrackException(exception, "AppDomain", fatal: e.IsTerminating);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TrackException(e.Exception, "TaskScheduler", reasonCode: "unobserved_task_exception");
    }

    private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
    {
        TrackEvent(TelemetryEvents.AppExited);
        ServiceProvider.Dispose();
    }

    private void RestoreAppTheme()
    {
        IThemeService themeService = ServiceProvider.GetService<IThemeService>();

        AppTheme defaultTheme = RequestedTheme == ApplicationTheme.Light ? AppTheme.Light : AppTheme.Dark;
        themeService.Initialize(defaultTheme);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs launchArgs)
    {
        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        Activate(args);
    }

    internal void Activate(AppActivationArguments args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            IActivationHandler activationHandler = ServiceProvider.GetService<IActivationHandler>();
            try
            {
                TrackEvent(
                    TelemetryEvents.AppActivated,
                    new Dictionary<string, object?>
                    {
                        [TelemetryAttributes.ActivationKind] = args.Kind.ToString()
                    });

                switch (args.Kind)
                {
                    case ExtendedActivationKind.Launch:
                        activationHandler.HandleLaunchActivationAsync();
                        break;

                    case ExtendedActivationKind.Protocol:
                        if (args.Data is global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)
                        {
                            activationHandler.HandleProtocolActivationAsync(protocolArgs.Uri);
                        }
                        else
                        {
                            ServiceProvider.GetService<ILogService>().LogWarning("Protocol activation data is not of expected type.");
                        }
                        break;

                    default:
                        ServiceProvider.GetService<ILogService>().LogWarning("Unexpected activation kind.");
                        break;
                }
            }
            catch (Exception e)
            {
                TrackException(e, "Activation", reasonCode: "activation_failed");
                ServiceProvider.GetService<ILogService>().LogException(e, "Activation failed.");
            }
        });
    }

    private void TrackEvent(
        string eventName,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ServiceProvider.GetService<ITelemetryService>().TrackEvent(eventName, attributes);
    }

    private void TrackException(
        Exception exception,
        string component,
        string? reasonCode = null,
        bool fatal = false)
    {
        ServiceProvider.GetService<ITelemetryService>().TrackException(
            exception,
            new TelemetryExceptionContext(
                Component: component,
                Fatal: fatal,
                ReasonCode: reasonCode));
    }
}
