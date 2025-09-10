using CaptureTool.Capture;
using CaptureTool.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Drawing;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class SelectionOverlayWindow : Window
{
    public SelectionOverlayWindowViewModel ViewModel => RootView.ViewModel;

    public bool IsClosed { get; private set; }

    public SelectionOverlayWindow(MonitorCaptureResult monitor, IEnumerable<Rectangle> monitorWindows, CaptureOptions options)
    {
        if (monitor.IsPrimary)
        {
            Activated += SelectionOverlayWindow_Activated;
            Closed += SelectionOverlayWindow_Closed;
        }

        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        var bounds = monitor.MonitorBounds;
        AppWindow.MoveAndResize(new(bounds.X, bounds.Y, bounds.Width, bounds.Height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.Maximize();
        }

        InitializeComponent();

        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.Load((monitor, monitorWindows, options));
        });
    }

    private void SelectionOverlayWindow_Closed(object sender, WindowEventArgs args)
    {
        Activated -= SelectionOverlayWindow_Activated;
        Closed -= SelectionOverlayWindow_Closed;

        ViewModel.Unload();
        Content = null;
        IsClosed = true;
    }

    private void SelectionOverlayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (!IsClosed && args.WindowActivationState != WindowActivationState.Deactivated)
                {
                    // Must call SetForegroundWindow or focus will not move to the new window on activation.
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    global::Windows.Win32.PInvoke.SetForegroundWindow(new(hwnd));
                    global::Windows.Win32.PInvoke.SetWindowDisplayAffinity(new(hwnd), global::Windows.Win32.UI.WindowsAndMessaging.WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);
                }
            }
            catch
            {
            }
        });
    }
}
