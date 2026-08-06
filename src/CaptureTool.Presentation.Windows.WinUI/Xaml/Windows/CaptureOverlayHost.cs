using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Features.CaptureOverlay;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Views;
using Microsoft.UI;
using Microsoft.UI.Content;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

internal sealed partial class CaptureOverlayHost : IDisposable
{
    private const int ToolbarTopInsetPixels = 14;
    private const double ProvisionalToolbarWidthDips = 468;
    private const double ProvisionalToolbarHeightDips = 48;

    private static readonly HWND HWND_TOPMOST = new(-1);
    private static readonly ConcurrentDictionary<nint, CaptureOverlayHost> _windowInstances = new();

    internal sealed partial class DestroyIconSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public DestroyIconSafeHandle(HINSTANCE hINSTANCE) : base(true)
        {
            handle = hINSTANCE;
        }

        protected override bool ReleaseHandle()
        {
            return PInvoke.DestroyIcon(new(handle));
        }
    }

    private HWND? _hwnd;
    private HWND? _shadowHwnd;
    private HWND? _borderHwnd;
    private DesktopWindowXamlSource? _xamlSource;
    private DesktopWindowXamlSource? _shadowXamlSource;
    private DesktopWindowXamlSource? _borderXamlSource;
    private CaptureOverlayView? _overlayView;
    private CaptureOverlayShadowView? _shadowView;
    private CaptureOverlayBorder? _borderControl;
    private Rectangle _monitorBounds;
    private CaptureOverlayWindowLayout _windowLayout;
    private double _layoutRasterizationScale;
    private bool _hasWindowLayout;
    private bool _showsToolbar = true;
    private bool _activationRequested;
    private bool _isToolbarShown;
    private bool _isShadowInitializationPending;
    private bool _isShadowReady;
    private bool _isInitialized;
    private bool _isClosed;

    private void CreateCaptureOverlayWindow(NewCaptureArgs args)
    {
        unsafe
        {
            MonitorCaptureResult monitor = args.Monitor;
            const string className = "CaptureOverlayWindow";

            WNDCLASSEXW wndClass = new()
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                style = 0,
                lpfnWndProc = &WindowProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = HINSTANCE.Null,
                hIcon = HICON.Null,
                hCursor = HCURSOR.Null,
                hbrBackground = HBRUSH.Null,
                lpszMenuName = null,
                hIconSm = HICON.Null
            };
            fixed (char* name = className)
            {
                wndClass.lpszClassName = name;
            }
            PInvoke.RegisterClassEx(in wndClass);

            int width = Math.Max(1, (int)Math.Ceiling(ProvisionalToolbarWidthDips * monitor.Scale));
            int height = Math.Max(1, (int)Math.Ceiling(ProvisionalToolbarHeightDips * monitor.Scale));
            int x = monitor.MonitorBounds.X +
                (int)Math.Floor((monitor.MonitorBounds.Width - (double)width) / 2d);

            _hwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
                className,
                null,
                WINDOW_STYLE.WS_POPUP,
                x,
                monitor.MonitorBounds.Y + ToolbarTopInsetPixels,
                width,
                height,
                new(IntPtr.Zero),
                null,
                new DestroyIconSafeHandle(wndClass.hInstance),
                null);

            EnsureWindowCreated(_hwnd.Value, className);
            if (!PInvoke.SetWindowDisplayAffinity(
                _hwnd.Value,
                WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to exclude the capture toolbar from screen capture.");
            }

            _xamlSource = new DesktopWindowXamlSource();
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hwnd.Value);
            _xamlSource.Initialize(windowId);
            ConfigureXamlSourceToFillWindow(_xamlSource);

            _overlayView = new CaptureOverlayView(args);
        }
    }

    private unsafe void TryCreateShadowWindow()
    {
        const string className = "CaptureOverlayWindowShadow";

        try
        {
            WNDCLASSEXW wndClass = new()
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                style = 0,
                lpfnWndProc = &ShadowWindowProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = HINSTANCE.Null,
                hIcon = HICON.Null,
                hCursor = HCURSOR.Null,
                hbrBackground = HBRUSH.Null,
                lpszMenuName = null,
                hIconSm = HICON.Null
            };
            fixed (char* name = className)
            {
                wndClass.lpszClassName = name;
            }
            PInvoke.RegisterClassEx(in wndClass);

            _shadowHwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_LAYERED |
                WINDOW_EX_STYLE.WS_EX_TOPMOST |
                WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                WINDOW_EX_STYLE.WS_EX_TRANSPARENT |
                WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
                className,
                null,
                WINDOW_STYLE.WS_POPUP,
                _monitorBounds.X,
                _monitorBounds.Y + ToolbarTopInsetPixels,
                1,
                1,
                HWND.Null,
                null,
                new DestroyIconSafeHandle(wndClass.hInstance),
                null);

            EnsureWindowCreated(_shadowHwnd.Value, className);
            if (!PInvoke.SetWindowDisplayAffinity(
                _shadowHwnd.Value,
                WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to exclude the toolbar shadow from screen capture.");
            }

            _shadowXamlSource = new DesktopWindowXamlSource();
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_shadowHwnd.Value);
            _shadowXamlSource.Initialize(windowId);
            ConfigureXamlSourceToFillWindow(_shadowXamlSource);

            _shadowView = new CaptureOverlayShadowView();
            _shadowView.InitializationCompleted += ShadowView_InitializationCompleted;
            _isShadowInitializationPending = true;
            _shadowXamlSource.Content = _shadowView;
            if (!_shadowView.QueueInitialization())
            {
                throw new InvalidOperationException("Failed to queue toolbar shadow initialization.");
            }
        }
        catch
        {
            // A shadow is cosmetic. Keep the exact-sized interactive toolbar if
            // the companion window or its composition surface cannot be created.
            DestroyShadowWindow();
        }
    }

    private void CreateCaptureOverlayBorderWindow(MonitorCaptureResult monitor, Rectangle area)
    {
        unsafe
        {
            const string borderClassName = "CaptureOverlayWindowBorder";

            WNDCLASSEXW borderWndClass = new()
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                style = 0,
                lpfnWndProc = &BorderWindowProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = HINSTANCE.Null,
                hIcon = HICON.Null,
                hCursor = HCURSOR.Null,
                hbrBackground = HBRUSH.Null,
                lpszMenuName = null,
                hIconSm = HICON.Null
            };
            fixed (char* name = borderClassName)
            {
                borderWndClass.lpszClassName = name;
            }
            PInvoke.RegisterClassEx(in borderWndClass);

            double scaling = monitor.Scale;
            int scaledX = (int)(area.X * scaling) + monitor.MonitorBounds.X;
            int scaledY = (int)(area.Y * scaling) + monitor.MonitorBounds.Y;
            int scaledWidth = (int)(area.Width * scaling);
            int scaledHeight = (int)(area.Height * scaling);

            _borderHwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_TRANSPARENT,
                borderClassName,
                null,
                WINDOW_STYLE.WS_VISIBLE | WINDOW_STYLE.WS_POPUP,
                scaledX,
                scaledY,
                scaledWidth,
                scaledHeight,
                new(IntPtr.Zero),
                null,
                new DestroyIconSafeHandle(borderWndClass.hInstance),
                null);

            EnsureWindowCreated(_borderHwnd.Value, borderClassName);
            if (!PInvoke.SetWindowDisplayAffinity(
                _borderHwnd.Value,
                WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to exclude the capture border from screen capture.");
            }

            _borderXamlSource = new DesktopWindowXamlSource();
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_borderHwnd.Value);
            _borderXamlSource.Initialize(windowId);
            ConfigureXamlSourceToFillWindow(_borderXamlSource);

            _borderControl = new CaptureOverlayBorder();
            _borderXamlSource.Content = _borderControl;
        }
    }

    public unsafe void Initialize(NewCaptureArgs args)
    {
        if (_isInitialized || _isClosed)
        {
            return;
        }

        var monitor = args.Monitor;
        var area = args.Area;
        _monitorBounds = monitor.MonitorBounds;

        try
        {
            CreateCaptureOverlayWindow(args);
            HWND hwnd = _hwnd ?? throw new InvalidOperationException("The capture toolbar window was not created.");
            CaptureOverlayView overlayView = _overlayView ?? throw new InvalidOperationException("The capture toolbar view was not created.");
            _windowInstances[(nint)hwnd.Value] = this;
            overlayView.SurfaceMetricsChanged += OverlayView_SurfaceMetricsChanged;
            overlayView.SurfaceMetricsInvalidated += OverlayView_SurfaceMetricsInvalidated;

            // Attaching the hidden island raises Loaded and queues the first safe
            // measurement. Calling Measure here initializes nested x:Bind trees too early.
            _xamlSource!.Content = overlayView;

            TryCreateShadowWindow();

            CreateCaptureOverlayBorderWindow(monitor, area);

            _isInitialized = true;
        }
        catch
        {
            Close();
            throw;
        }
    }

    public void Activate()
    {
        if (_isClosed || _hwnd == null)
        {
            return;
        }

        _activationRequested = true;
        TryShowAndActivateToolbar();
    }

    private void TryShowAndActivateToolbar()
    {
        if (!_activationRequested ||
            !_hasWindowLayout ||
            _hwnd == null ||
            (_showsToolbar && _isShadowInitializationPending) ||
            !ApplyWindowLayout(showWindow: true))
        {
            return;
        }

        if (_borderHwnd != null)
        {
            Win32WindowHelpers.SetActiveWindow(_borderHwnd.Value);
        }

        Win32WindowHelpers.SetActiveWindow(_hwnd.Value);
        Win32WindowHelpers.SetForegroundWindow(_hwnd.Value);

        // Foreground activation can change the order of topmost windows. Put the
        // unowned shadow immediately behind the toolbar again afterwards.
        PositionShadowWindow(showWindow: true);
    }

    public void Close()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        _activationRequested = false;

        HideSurfaceWindows();

        if (_overlayView != null)
        {
            _overlayView.SurfaceMetricsChanged -= OverlayView_SurfaceMetricsChanged;
            _overlayView.SurfaceMetricsInvalidated -= OverlayView_SurfaceMetricsInvalidated;
        }

        DestroyShadowWindow();
        DestroyBorderWindow();
        DestroyOverlayWindow();
    }

    public void HideBorder()
    {
        DestroyBorderWindow();
    }

    private void OverlayView_SurfaceMetricsChanged(
        object? sender,
        CaptureOverlaySurfaceMetrics metrics)
    {
        if (_isClosed)
        {
            return;
        }

        UpdateWindowLayout(metrics);
    }

    private void OverlayView_SurfaceMetricsInvalidated(object? sender, EventArgs e)
    {
        if (!_isClosed)
        {
            HideSurfaceWindows();
        }
    }

    private void ShadowView_InitializationCompleted(
        object? sender,
        CaptureOverlayShadowInitializationEventArgs e)
    {
        if (_isClosed || !ReferenceEquals(sender, _shadowView))
        {
            return;
        }

        _isShadowInitializationPending = false;
        _isShadowReady = e.Succeeded;
        if (!e.Succeeded)
        {
            DestroyShadowWindow();
        }

        if (_activationRequested && !_isToolbarShown)
        {
            TryShowAndActivateToolbar();
        }
        else
        {
            PositionShadowWindow(_isToolbarShown);
        }
    }

    private void UpdateWindowLayout(CaptureOverlaySurfaceMetrics metrics)
    {
        if (!CaptureOverlayWindowGeometry.TryCalculate(
            _monitorBounds,
            metrics.Width,
            metrics.Height,
            metrics.RasterizationScale,
            ToolbarTopInsetPixels,
            CaptureOverlayShadowView.ShadowPaddingDips,
            out CaptureOverlayWindowLayout layout))
        {
            HideSurfaceWindows();
            return;
        }

        bool physicalLayoutChanged =
            !_hasWindowLayout ||
            layout != _windowLayout ||
            metrics.ShowsToolbar != _showsToolbar;
        bool casterScaleChanged =
            !_hasWindowLayout ||
            metrics.RasterizationScale != _layoutRasterizationScale;

        _windowLayout = layout;
        _layoutRasterizationScale = metrics.RasterizationScale;
        _hasWindowLayout = true;
        _showsToolbar = metrics.ShowsToolbar;

        if (physicalLayoutChanged || casterScaleChanged)
        {
            Point casterOffset = layout.ShadowCasterOffset;
            _shadowView?.UpdateCaster(
                casterOffset.X,
                casterOffset.Y,
                layout.ToolbarBounds.Width,
                layout.ToolbarBounds.Height,
                metrics.RasterizationScale);
        }

        if (!physicalLayoutChanged)
        {
            if (_activationRequested && !_isToolbarShown)
            {
                TryShowAndActivateToolbar();
            }
            return;
        }

        if (_activationRequested && !_isToolbarShown)
        {
            TryShowAndActivateToolbar();
        }
        else
        {
            ApplyWindowLayout(_activationRequested);
        }
    }

    private bool ApplyWindowLayout(bool showWindow)
    {
        if (!_hasWindowLayout || _hwnd == null)
        {
            return false;
        }

        Rectangle toolbarBounds = _windowLayout.ToolbarBounds;
        SET_WINDOW_POS_FLAGS toolbarFlags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        toolbarFlags |= showWindow
            ? SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW
            : SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW;

        if (!PInvoke.SetWindowPos(
            _hwnd.Value,
            HWND_TOPMOST,
            toolbarBounds.X,
            toolbarBounds.Y,
            toolbarBounds.Width,
            toolbarBounds.Height,
            toolbarFlags))
        {
            HideSurfaceWindows();
            return false;
        }

        _isToolbarShown = showWindow;
        PositionShadowWindow(showWindow);
        return true;
    }

    private void PositionShadowWindow(bool showWindow)
    {
        if (!_hasWindowLayout || _hwnd == null || _shadowHwnd == null)
        {
            return;
        }

        Rectangle shadowBounds = _windowLayout.ShadowBounds;
        SET_WINDOW_POS_FLAGS shadowFlags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        shadowFlags |= showWindow && _showsToolbar && _isShadowReady
            ? SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW
            : SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW;

        if (!PInvoke.SetWindowPos(
            _shadowHwnd.Value,
            _hwnd.Value,
            shadowBounds.X,
            shadowBounds.Y,
            shadowBounds.Width,
            shadowBounds.Height,
            shadowFlags))
        {
            DestroyShadowWindow();
        }
    }

    private void HideSurfaceWindows()
    {
        _isToolbarShown = false;

        if (_shadowHwnd != null)
        {
            try
            {
                PInvoke.ShowWindow(_shadowHwnd.Value, SHOW_WINDOW_CMD.SW_HIDE);
            }
            catch { }
        }

        if (_hwnd != null)
        {
            try
            {
                PInvoke.ShowWindow(_hwnd.Value, SHOW_WINDOW_CMD.SW_HIDE);
            }
            catch { }
        }
    }

    private void DestroyShadowWindow()
    {
        _isShadowInitializationPending = false;
        _isShadowReady = false;

        CaptureOverlayShadowView? shadowView = _shadowView;
        _shadowView = null;
        try
        {
            if (shadowView != null)
            {
                shadowView.InitializationCompleted -= ShadowView_InitializationCompleted;
                shadowView.Dispose();
            }
        }
        catch { }

        DesktopWindowXamlSource? shadowXamlSource = _shadowXamlSource;
        _shadowXamlSource = null;
        if (shadowXamlSource != null)
        {
            try
            {
                shadowXamlSource.Content = null;
            }
            catch { }

            try
            {
                (shadowXamlSource as IDisposable)?.Dispose();
            }
            catch { }
        }

        HWND? shadowHwnd = _shadowHwnd;
        _shadowHwnd = null;
        if (shadowHwnd != null)
        {
            try
            {
                PInvoke.DestroyWindow(shadowHwnd.Value);
            }
            catch { }
        }
    }

    private unsafe void DestroyOverlayWindow()
    {
        CaptureOverlayView? overlayView = _overlayView;
        _overlayView = null;
        if (overlayView != null)
        {
            try
            {
                overlayView.SurfaceMetricsChanged -= OverlayView_SurfaceMetricsChanged;
                overlayView.SurfaceMetricsInvalidated -= OverlayView_SurfaceMetricsInvalidated;
                overlayView.ViewModel?.Dispose();
            }
            catch { }
        }

        DesktopWindowXamlSource? xamlSource = _xamlSource;
        _xamlSource = null;
        if (xamlSource != null)
        {
            try
            {
                xamlSource.Content = null;
            }
            catch { }

            try
            {
                (xamlSource as IDisposable)?.Dispose();
            }
            catch { }
        }

        HWND? hwnd = _hwnd;
        _hwnd = null;
        if (hwnd != null)
        {
            _windowInstances.TryRemove((nint)hwnd.Value.Value, out _);
            try
            {
                PInvoke.DestroyWindow(hwnd.Value);
            }
            catch { }
        }
    }

    private void DestroyBorderWindow()
    {
        _borderControl = null;

        DesktopWindowXamlSource? borderXamlSource = _borderXamlSource;
        _borderXamlSource = null;
        if (borderXamlSource != null)
        {
            try
            {
                borderXamlSource.Content = null;
            }
            catch { }

            try
            {
                (borderXamlSource as IDisposable)?.Dispose();
            }
            catch { }
        }

        HWND? borderHwnd = _borderHwnd;
        _borderHwnd = null;
        if (borderHwnd != null)
        {
            try
            {
                PInvoke.DestroyWindow(borderHwnd.Value);
            }
            catch { }
        }
    }

    private static unsafe void EnsureWindowCreated(HWND hwnd, string className)
    {
        if ((nint)hwnd.Value == IntPtr.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Failed to create the {className} window.");
        }
    }

    private static void ConfigureXamlSourceToFillWindow(DesktopWindowXamlSource xamlSource)
    {
        if (xamlSource.SiteBridge is DesktopChildSiteBridge childSiteBridge)
        {
            childSiteBridge.ResizePolicy = ContentSizePolicy.ResizeContentToParentWindow;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static unsafe LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        const uint WM_ACTIVATE = 0x0006;
        const int WA_INACTIVE = 0;

        LRESULT result = PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
        if (msg == WM_ACTIVATE &&
            ((int)wParam.Value & 0xFFFF) != WA_INACTIVE &&
            _windowInstances.TryGetValue((nint)hwnd.Value, out CaptureOverlayHost? host) &&
            !host._isClosed)
        {
            host.PositionShadowWindow(host._isToolbarShown);
        }

        return result;
    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static LRESULT BorderWindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static LRESULT ShadowWindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        const uint WM_MOUSEACTIVATE = 0x0021;
        const uint WM_NCHITTEST = 0x0084;
        const int MA_NOACTIVATE = 3;
        const int HTTRANSPARENT = -1;

        return msg switch
        {
            WM_NCHITTEST => new LRESULT(HTTRANSPARENT),
            WM_MOUSEACTIVATE => new LRESULT(MA_NOACTIVATE),
            _ => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam)
        };
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }
}
