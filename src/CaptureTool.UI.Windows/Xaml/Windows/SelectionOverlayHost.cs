using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Domains.Capture.Implementations.Windows;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.UI.Windows.Utils;
using Microsoft.UI.Xaml;
using System.Drawing;
using Windows.Win32;

namespace CaptureTool.UI.Windows.Xaml.Windows;

internal sealed partial class SelectionOverlayHost : IDisposable
{
#if DEBUG
    private static readonly bool SingleMonitorNoWindowWatcher = false;
#endif

    private readonly HashSet<MonitorCaptureResult> _monitors = [];
    private readonly HashSet<SelectionOverlayWindow> _windows = [];
    private readonly HashSet<nint> _windowHandles = [];
    private ISelectionOverlayHostViewModel? _viewModel;
    private DispatcherTimer? _foregroundTimer;
    private Window? _primaryWindow;
    private bool _disposed;

    public event EventHandler? LostFocus;

    public MonitorCaptureResult[] GetMonitors()
    {
        return [.. _monitors];
    }

    public void UpdateOptions(CaptureOptions options)
    {
        _viewModel?.UpdateOptions(options);
    }

    public void Initialize(CaptureOptions options)
    {
        if (_viewModel != null)
        {
            return;
        }
        _viewModel = ViewModelLocator.GetViewModel<ISelectionOverlayHostViewModel>();
        _viewModel.AllScreensCaptureRequested += OnAllScreensCaptureRequested;

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
            Rectangle monitorBounds = monitor.MonitorBounds;
            float scale = monitor.Scale;
            IEnumerable<Rectangle> monitorWindows = allWindows
                .Select(w => w.Position)
                .Where(p =>
                    monitorBounds.IntersectsWith(p) ||
                    monitorBounds.Contains(p))
                .Select(r => new Rectangle(
                    (int)((r.X - monitorBounds.X) / scale),
                    (int)((r.Y - monitorBounds.Y) / scale),
                    (int)(r.Width / scale),
                    (int)(r.Height / scale)));

            SelectionOverlayWindowOptions overlayOptions = new(monitor, [.. monitorWindows], options);
            SelectionOverlayWindow window = new(overlayOptions);
            _windows.Add(window);

            nint hwnd = window.GetWindowHandle();
            _windowHandles.Add(hwnd);

            _viewModel.AddWindowViewModel(window.ViewModel, monitor.IsPrimary);
            if (monitor.IsPrimary)
            {
                _primaryWindow = window;
                _primaryWindow.Activated += OnPrimaryWindowActivated;
            }
        }
    }

    private void OnAllScreensCaptureRequested(object? sender, EventArgs e)
    {
        ImageFile image = AppServiceLocator.ImageCapture.PerformMultiMonitorImageCapture([.. _monitors]);
        AppServiceLocator.Navigation.GoToImageEdit(image);
    }

    public void Activate()
    {
        if (_viewModel == null)
        {
            return;
        }

        foreach (var window in _windows)
        {
            window.Activate();
        }

        _primaryWindow?.Activate();
    }

    public void Close()
    {
        if (_disposed)
        {
            return;
        }

        StopForegroundMonitor();

        if (_primaryWindow != null)
        {
            _primaryWindow.Activated -= OnPrimaryWindowActivated;
            _primaryWindow = null;
        }

        if (_viewModel != null)
        {
            _viewModel.AllScreensCaptureRequested -= OnAllScreensCaptureRequested;
            _viewModel.Dispose();
            _viewModel = null;
        }

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
        _windowHandles.Clear();
        _monitors.Clear();
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

        _foregroundTimer.Tick += OnForegroundTimerTick;
        _foregroundTimer.Start();
    }

    private void OnForegroundTimerTick(object? sender, object e)
    {
        var foregroundHwnd = PInvoke.GetForegroundWindow();
        if (!_windowHandles.Contains(foregroundHwnd))
        {
            LostFocus?.Invoke(this, EventArgs.Empty);
            Close();
        }
    }

    private void StopForegroundMonitor()
    {
        if (_foregroundTimer != null)
        {
            _foregroundTimer.Tick -= OnForegroundTimerTick;
            _foregroundTimer.Stop();
            _foregroundTimer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Close();
        GC.SuppressFinalize(this);
    }
}
