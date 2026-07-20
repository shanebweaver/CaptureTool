using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Presentation.Windows.WinUI.UiTests;
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
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();
        ServiceProvider = new();
        InitializeComponent();
        RestoreAppTheme();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        ServiceProvider.GetService<ILogService>().LogException(e.Exception, "Unhandled exception occurred.");
    }

    private void RestoreAppTheme()
    {
        IThemeService themeService = ServiceProvider.GetService<IThemeService>();

        AppTheme defaultTheme = RequestedTheme == ApplicationTheme.Light ? AppTheme.Light : AppTheme.Dark;
        themeService.Initialize(defaultTheme);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs launchArgs)
    {
        if (UiTestLaunchOptions.Current.IsEnabled)
        {
            IActivationHandler activationHandler = ServiceProvider.GetService<IActivationHandler>();
            await activationHandler.HandleLaunchActivationAsync();
            return;
        }

        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (TryMaterializeActivation(args, out MaterializedActivation activation))
        {
            await ActivateAsync(activation);
        }
    }

    internal void Activate(AppActivationArguments args)
    {
        // Redirected activation data can be backed by a COM proxy to the process
        // performing the redirect. Materialize it before this callback returns and
        // that process is allowed to exit.
        if (!TryMaterializeActivation(args, out MaterializedActivation activation))
        {
            return;
        }

        bool enqueued = DispatcherQueue.TryEnqueue(async () =>
        {
            await ActivateAsync(activation);
        });

        if (!enqueued)
        {
            ServiceProvider.GetService<ILogService>().LogWarning("Failed to enqueue activation on the UI thread.");
        }
    }

    private bool TryMaterializeActivation(
        AppActivationArguments args,
        out MaterializedActivation activation)
    {
        try
        {
            ExtendedActivationKind kind = args.Kind;
            Uri? protocolUri = null;

            if (kind == ExtendedActivationKind.Protocol)
            {
                if (args.Data is not global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)
                {
                    ServiceProvider.GetService<ILogService>().LogWarning("Protocol activation data is not of expected type.");
                    activation = default;
                    return false;
                }

                protocolUri = new Uri(protocolArgs.Uri.AbsoluteUri);
            }

            activation = new MaterializedActivation(kind, protocolUri);
            return true;
        }
        catch (Exception e)
        {
            ServiceProvider.GetService<ILogService>().LogException(e, "Failed to read activation data.");
            activation = default;
            return false;
        }
    }

    private async Task ActivateAsync(MaterializedActivation activation)
    {
        IActivationHandler activationHandler = ServiceProvider.GetService<IActivationHandler>();
        try
        {
            switch (activation.Kind)
            {
                case ExtendedActivationKind.Launch:
                    await activationHandler.HandleLaunchActivationAsync();
                    break;

                case ExtendedActivationKind.Protocol:
                    if (activation.ProtocolUri is not null)
                    {
                        await activationHandler.HandleProtocolActivationAsync(activation.ProtocolUri);
                    }
                    else
                    {
                        ServiceProvider.GetService<ILogService>().LogWarning("Protocol activation URI is unavailable.");
                    }
                    break;

                default:
                    ServiceProvider.GetService<ILogService>().LogWarning("Unexpected activation kind.");
                    break;
            }
        }
        catch (Exception e)
        {
            ServiceProvider.GetService<ILogService>().LogException(e, "Activation failed.");
        }
    }

    private readonly record struct MaterializedActivation(
        ExtendedActivationKind Kind,
        Uri? ProtocolUri);
}
