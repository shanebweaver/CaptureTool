using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Infrastructure.Analysis.FoundryLocal;
using CaptureTool.Presentation.Activation;
using CaptureTool.Presentation.Windows.WinUI.Activation;
using CaptureTool.Presentation.Windows.WinUI.UiTests;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.Presentation.Windows.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    internal new static App Current => (App)Microsoft.UI.Xaml.Application.Current;

    private readonly StartupActivationQueue<ActivationMaterializationResult> _redirectedActivations;

    internal AppServiceProvider ServiceProvider { get; }
    internal DispatcherQueue DispatcherQueue { get; }

    public App() : this(new StartupActivationQueue<ActivationMaterializationResult>())
    {
    }

    internal App(StartupActivationQueue<ActivationMaterializationResult> redirectedActivations)
    {
        _redirectedActivations = redirectedActivations;
        UnhandledException += App_UnhandledException;
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();
        ServiceProvider = new();
        global::Windows.System.MemoryManager.AppMemoryUsageIncreased +=
            MemoryManager_AppMemoryUsageIncreased;
        InitializeComponent();
        RestoreAppTheme();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        ServiceProvider.GetService<ILogService>().LogException(e.Exception, "Unhandled exception occurred.");
    }

    private async void MemoryManager_AppMemoryUsageIncreased(object? sender, object e)
    {
        if (global::Windows.System.MemoryManager.AppMemoryUsageLevel is not (
            global::Windows.System.AppMemoryUsageLevel.High or
            global::Windows.System.AppMemoryUsageLevel.OverLimit))
        {
            return;
        }

        try
        {
            IEnumerable<IFoundryLocalSpeechTranscriptionService> transcriptionServices =
                ServiceProvider.GetServices<IFoundryLocalSpeechTranscriptionService>();
            foreach (IFoundryLocalSpeechTranscriptionService transcriptionService in
                transcriptionServices)
            {
                await transcriptionService.ReleaseModelAsync();
            }
        }
        catch (ObjectDisposedException)
        {
            // Application teardown won the race with a late memory-pressure event.
        }
        catch (Exception exception)
        {
            ServiceProvider.GetService<ILogService>().LogException(
                exception,
                "Failed to release the Foundry Local model under memory pressure.");
        }
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
        await HandleMaterializedActivationAsync(ActivationMaterializer.Materialize(args));
        _redirectedActivations.Attach(Activate);
    }

    internal void Activate(ActivationMaterializationResult activation)
    {
        bool enqueued = DispatcherQueue.TryEnqueue(async () =>
        {
            await HandleMaterializedActivationAsync(activation);
        });

        if (!enqueued)
        {
            ServiceProvider.GetService<ILogService>().LogWarning("Failed to enqueue activation on the UI thread.");
        }
    }

    private async Task HandleMaterializedActivationAsync(ActivationMaterializationResult result)
    {
        if (result.Activation is MaterializedActivation activation)
        {
            await ActivateAsync(activation);
            return;
        }

        string message = result.FailureMessage ?? "Activation data is unavailable.";
        if (result.FailureException is Exception exception)
        {
            ServiceProvider.GetService<ILogService>().LogException(exception, message);
        }
        else
        {
            ServiceProvider.GetService<ILogService>().LogWarning(message);
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
}
