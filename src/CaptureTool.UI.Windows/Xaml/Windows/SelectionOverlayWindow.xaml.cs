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
        Activated += OnActivated;
        Closed += OnClosed;
        if (monitor.IsPrimary)
        {
            Activated += OnPrimaryActivated;
            Closed += OnPrimaryClosed;
        }

        var bounds = monitor.MonitorBounds;
        AppWindow.MoveAndResize(new(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        
        EnsureMaximized();
        InitializeComponent();

        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.Load((monitor, monitorWindows, options));
        });
    }

    private void EnsureMaximized()
    {
        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.Maximize();
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        EnsureMaximized();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;

        ViewModel.Dispose();

        Content = null;
        IsClosed = true;
    }

    private void OnPrimaryActivated(object sender, WindowActivatedEventArgs args)
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
    }

    private void OnPrimaryClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnPrimaryActivated;
        Closed -= OnPrimaryClosed;
    }
}
