using CaptureTool.Core.AppController;
using CaptureTool.Services.Themes;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;

namespace CaptureTool.UI.Windows;

public partial class App : Application
{
    internal new static App Current => (App)Application.Current;

    internal CaptureToolServiceProvider ServiceProvider { get; }
    internal DispatcherQueue DispatcherQueue { get; }

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
            IAppController appController = ServiceProvider.GetService<IAppController>();
            try
            {
                switch (args.Kind)
                {
                    case ExtendedActivationKind.Launch:
                        appController.HandleLaunchActicationAsync();
                        break;
                    case ExtendedActivationKind.Protocol:
                        if (args.Data is global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)
                        {
                            appController.HandleProtocolActivationAsync(protocolArgs.Uri);
                        }
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected activation kind");
                }
            }
            catch (Exception e)
            {
                ServiceLocator.Logging.LogException(e, "Activation failed.");
                appController.Shutdown();
            }
        });
    }
}
