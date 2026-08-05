using CaptureTool.Presentation.Activation;
using CaptureTool.Presentation.Windows.WinUI.Activation;
using CaptureTool.Presentation.Windows.WinUI.UiTests;
using Microsoft.UI.Dispatching;
using Microsoft.Win32.SafeHandles;
using Microsoft.Windows.AppLifecycle;
using System.Diagnostics;
using Windows.Win32.Foundation;

namespace CaptureTool.Presentation.Windows.WinUI;

// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance
public class Program
{
    private const uint InfiniteTimeout = uint.MaxValue;
    private const uint DefaultFlags = 0;

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
                RedirectToPrimary(instance);
                return 0;
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

    private static void RedirectToPrimary(AppInstance primary)
    {
        // Create an unnamed manual-reset event (initially nonsignaled)
        SafeFileHandle safeEvt = global::Windows.Win32.PInvoke.CreateEvent(
            lpEventAttributes: null,
            bManualReset: true,
            bInitialState: false,
            lpName: null);

        // Wrap raw HANDLE for CsWin32 calls
        var evtHandle = new HANDLE(safeEvt.DangerousGetHandle());

        // Perform redirection on background thread
        _ = Task.Run(() =>
        {
            primary.RedirectActivationToAsync(
                AppInstance.GetCurrent().GetActivatedEventArgs())
                .AsTask().GetAwaiter().GetResult();

            // Signal completion
            global::Windows.Win32.PInvoke.SetEvent(evtHandle);
        });

        // Pump COM and wait for the event
        Span<HANDLE> handles = [evtHandle];
        global::Windows.Win32.PInvoke.CoWaitForMultipleObjects(
            dwFlags: DefaultFlags,
            dwTimeout: InfiniteTimeout,
            pHandles: handles,
            out _);

        // Dispose event handle
        safeEvt.Dispose();

        // Bring primary window to foreground
        var proc = Process.GetProcessById((int)primary.ProcessId);
        global::Windows.Win32.PInvoke.SetForegroundWindow(new HWND(proc.MainWindowHandle));
    }
}
