using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.UI;

public partial class App : Application
{
    public new static App Current => (App)Application.Current;
 
    public Ioc Ioc => m_ioc;

    private readonly Ioc m_ioc;
    private Window? m_window;

    public App()
    {
        m_ioc = new();
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs launchArgs)
    {
        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        _ = ActivateAsync(args, CancellationToken.None); // TODO: Prevent competing activations
    }

    public async Task ActivateAsync(AppActivationArguments args, CancellationToken cancellationToken)
    {
        try
        {
            await m_ioc.InitializeServicesAsync(cancellationToken);

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
        m_window ??= new MainWindow();
        m_window.Activate();
    }

    private static void CheckExit()
    {
        if (Window.Current == null)
        {
            Application.Current.Exit();
        }
    }
}
