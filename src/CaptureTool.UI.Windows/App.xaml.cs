using CaptureTool.Services.Interfaces.Activation;
using CaptureTool.Services.Interfaces.Themes;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.UI.Windows;

public partial class App : Application
{
    internal new static App Current => (App)Application.Current;

    internal CaptureToolServiceProvider ServiceProvider { get; }
    internal DispatcherQueue DispatcherQueue { get; }

    private readonly KeepAlive _keepAlive;

    public App()
    {
        UnhandledException += App_UnhandledException;
        ServiceProvider = new();
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();
        InitializeComponent();
        RestoreAppTheme();
        _keepAlive = new KeepAlive();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        ServiceLocator.Logging.LogException(e.Exception, "Unhandled exception occurred.");
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
                            ServiceLocator.Logging.LogWarning("Protocol activation data is not of expected type.");
                        }
                        break;

                    default:
                        ServiceLocator.Logging.LogWarning("Unexpected activation kind.");
                        break;
                }
            }
            catch (Exception e)
            {
                ServiceLocator.Logging.LogException(e, "Activation failed.");
            }
        });
    }
}
