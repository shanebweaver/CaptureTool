using CaptureTool.Infrastructure.Windows.Activation;
using CaptureTool.Presentation.Activation;
using CaptureTool.Presentation.Windows.WinUI.Activation;
using CaptureTool.Presentation.Windows.WinUI.UiTests;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using System.Diagnostics;
using Windows.Win32.Foundation;

namespace CaptureTool.Presentation.Windows.WinUI;

// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance
public class Program
{
    private static readonly TimeSpan RedirectTimeout = TimeSpan.FromSeconds(30);

    [STAThread]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "App entry point")]
    public static int Main(string[] args)
    {
        UiTestLaunchOptions.Initialize(args);
        var redirectedActivations = new StartupActivationQueue<ActivationMaterializationResult>();

        // Initialize COM wrappers and single-instance
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (!UiTestLaunchOptions.Current.IsEnabled)
        {
            var instance = AppInstance.FindOrRegisterForKey(UiTestLaunchOptions.DefaultInstanceKey);

            if (!instance.IsCurrent)
            {
                return RedirectToPrimary(instance);
            }

            // Materialize redirected data before the redirecting process can exit,
            // then retain it until the primary launch and UI dispatcher are ready.
            instance.Activated += (_, e) => redirectedActivations.Enqueue(
                ActivationMaterializer.Materialize(e));
        }

        // Start WinUI app
        Microsoft.UI.Xaml.Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread()));
            var app = new App(redirectedActivations);
        });

        return 0;
    }

    private static int RedirectToPrimary(AppInstance primary)
    {
        try
        {
            ActivationRedirector.Redirect(() => primary.RedirectActivationToAsync(
                AppInstance.GetCurrent().GetActivatedEventArgs()).AsTask(), RedirectTimeout);
        }
        catch (Exception exception)
        {
            // There is no App or log service in the redirecting process.
            Trace.TraceError($"Activation redirection failed: {exception}");
            return 1;
        }

        try
        {
            using var process = Process.GetProcessById((int)primary.ProcessId);
            global::Windows.Win32.PInvoke.SetForegroundWindow(new HWND(process.MainWindowHandle));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The primary can exit after accepting the activation.
            Trace.TraceWarning($"Could not foreground the primary instance: {exception}");
        }

        return 0;
    }
}
