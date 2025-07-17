using CaptureTool.Capture;
using CaptureTool.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class CaptureOverlayWindow : Window
{
    public CaptureOverlayWindowViewModel ViewModel => RootView.ViewModel;

    public CaptureOverlayWindow(MonitorCaptureResult monitor, IEnumerable<Rectangle> monitorWindows)
    {
        if (monitor.IsPrimary)
        {
            Activated += CaptureOverlayWindow_Activated;
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

        ViewModel.CloseRequested += ViewModel_CloseRequested;
        ViewModel.Load(monitor, monitorWindows);
    }

    private void ViewModel_CloseRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(Close);
    }

    private void CaptureOverlayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                // Must call SetForegroundWindow or focus will not move to the new window on activation.
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                global::Windows.Win32.PInvoke.SetForegroundWindow(new(hwnd));
            }
        });
    }
}
