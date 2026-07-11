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
        await ActivateAsync(args);
    }

    internal void Activate(AppActivationArguments args)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await ActivateAsync(args);
        });
    }

    private async Task ActivateAsync(AppActivationArguments args)
    {
        IActivationHandler activationHandler = ServiceProvider.GetService<IActivationHandler>();
        try
        {
            switch (args.Kind)
            {
                case ExtendedActivationKind.Launch:
                    await activationHandler.HandleLaunchActivationAsync();
                    break;

                case ExtendedActivationKind.Protocol:
                    if (args.Data is global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)
                    {
                        await activationHandler.HandleProtocolActivationAsync(protocolArgs.Uri);
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
            ServiceProvider.GetService<ILogService>().LogException(e, "Activation failed.");
        }
    }
}
