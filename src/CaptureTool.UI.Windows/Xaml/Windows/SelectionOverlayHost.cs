using CaptureTool.Capture;
using CaptureTool.Capture.Windows;
using CaptureTool.ViewModels;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Windows.Win32;
using WinRT.Interop;

namespace CaptureTool.UI.Windows.Xaml.Windows;

internal sealed partial class SelectionOverlayHost : IDisposable
{
#if DEBUG
    private static readonly bool SingleMonitorNoWindowWatcher = true;
#endif

    private readonly Action _onClosed;

    private readonly HashSet<MonitorCaptureResult> _monitors = [];
    private readonly HashSet<SelectionOverlayWindow> _windows = [];
    private readonly HashSet<nint> _windowHandles = [];
    private SelectionOverlayHostViewModel? _viewModel;
    private DispatcherTimer? _foregroundTimer;
    private Window? _primaryWindow;

    public MonitorCaptureResult[] GetMonitors()
    {
        return [.. _monitors];
    }

    public SelectionOverlayHost(Action onClosed)
    {
        _onClosed = onClosed;
    }

    public void Show(CaptureOptions options)
    {
        Close();
        _viewModel = ViewModelLocator.GetViewModel<SelectionOverlayHostViewModel>();

        var allWindows = WindowInfoHelper.GetAllWindows();
        var monitors = MonitorCaptureHelper.CaptureAllMonitors();

        foreach (var monitor in monitors)
        {
#if DEBUG
            if (SingleMonitorNoWindowWatcher && !monitor.IsPrimary)
                continue;
#endif

            _monitors.Add(monitor);

            // Scale window dimensions per monitor.
            var monitorBounds = monitor.MonitorBounds;
            var scale = monitor.Scale;
            var monitorWindows = allWindows
                .Select(w => w.Position)
                .Where(p =>
                    monitorBounds.IntersectsWith(p) ||
                    monitorBounds.Contains(p))
                .Select(r => new Rectangle(
                    (int)((r.X - monitorBounds.X) / scale),
                    (int)((r.Y - monitorBounds.Y) / scale),
                    (int)(r.Width / scale),
                    (int)(r.Height / scale)));

            var window = new SelectionOverlayWindow(monitor, [.. monitorWindows], options);
            _windows.Add(window);

            var hwnd = WindowNative.GetWindowHandle(window);
            _windowHandles.Add(hwnd);

            _viewModel.AddWindowViewModel(window.ViewModel);

            if (window.ViewModel.IsPrimary)
            {
                _primaryWindow = window;
                _primaryWindow.Activated += OnPrimaryWindowActivated;
            }
        }

        _primaryWindow?.Activate();
    }

    public void TransitionToVideoMode(MonitorCaptureResult monitor, Rectangle area)
    {
        _viewModel?.TransitionToVideoMode(monitor, area);
    }

    private void OnPrimaryWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (sender is Window window)
        {
            window.Activated -= OnPrimaryWindowActivated;
            StartForegroundWindowWatcher();
        }
    }

    private void StartForegroundWindowWatcher()
    {
#if DEBUG
        if (SingleMonitorNoWindowWatcher)
        {
            return;
        }
#endif

        if (_foregroundTimer != null)
        {
            return;
        }

        _foregroundTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };

        _foregroundTimer.Tick += (_, _) =>
        {
            var foregroundHwnd = PInvoke.GetForegroundWindow();
            if (!_windowHandles.Contains(foregroundHwnd))
            {
                Close();
                _onClosed.Invoke();
            }
        };

        _foregroundTimer.Start();
    }

    private void StopForegroundMonitor()
    {
        _foregroundTimer?.Stop();
        _foregroundTimer = null;
    }

    public void Close()
    {
        StopForegroundMonitor();
        _windowHandles.Clear();
        _monitors.Clear();

        _viewModel?.Unload();
        _viewModel = null;

        foreach (SelectionOverlayWindow window in _windows)
        {
            try
            {
                if (!window.IsClosed)
                {
                    window.DispatcherQueue.EnqueueAsync(() => window.Close());
                }
            }
            catch (Exception){ }
        }

        _windows.Clear();
    }

    public void Dispose()
    {
        Close();
    }
}
