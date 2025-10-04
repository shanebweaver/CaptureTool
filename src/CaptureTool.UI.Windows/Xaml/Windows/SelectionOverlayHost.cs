using CaptureTool.Capture;
using CaptureTool.Capture.Windows;
using CaptureTool.ViewModels;
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
    private static readonly bool SingleMonitorNoWindowWatcher = false;
#endif

    private readonly HashSet<MonitorCaptureResult> _monitors = [];
    private readonly HashSet<SelectionOverlayWindow> _windows = [];
    private readonly HashSet<nint> _windowHandles = [];
    private SelectionOverlayHostViewModel? _viewModel;
    private DispatcherTimer? _foregroundTimer;
    private Window? _primaryWindow;

    public event EventHandler? LostFocus;

    public MonitorCaptureResult[] GetMonitors()
    {
        return [.. _monitors];
    }

    public void Show(CaptureOptions options)
    {
        if (_viewModel != null)
        {
            return;
        }
        _viewModel = ViewModelLocator.GetViewModel<SelectionOverlayHostViewModel>();

        var allWindows = WindowInfoHelper.GetAllWindows();
        var monitors = MonitorCaptureHelper.CaptureAllMonitors();

        foreach (var monitor in monitors)
        {
#if DEBUG
            if (SingleMonitorNoWindowWatcher && !monitor.IsPrimary)
            {
                continue;
            }
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

            if (monitor.IsPrimary)
            {
                _primaryWindow = window;
                _primaryWindow.Activated += OnPrimaryWindowActivated;
            }
        }

        _primaryWindow?.Activate();
    }

    public void Close()
    {
        if (_viewModel == null)
        {
            return;
        }

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
                    window.DispatcherQueue.TryEnqueue(window.Close);
                }
            }
            catch (Exception) { }
        }

        _windows.Clear();
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
                LostFocus?.Invoke(this, EventArgs.Empty);
            }
        };

        _foregroundTimer.Start();
    }

    private void StopForegroundMonitor()
    {
        _foregroundTimer?.Stop();
        _foregroundTimer = null;
    }

    public void Dispose()
    {
        _foregroundTimer?.Stop();
        _foregroundTimer = null;

        _windowHandles.Clear();
        _monitors.Clear();

        _viewModel?.Unload();
        _viewModel = null;

        _primaryWindow = null;
        _windows.Clear();
    }
}
