using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.UI.Windows.Utils;
using CaptureTool.UI.Windows.Xaml.Controls;
using CaptureTool.UI.Windows.Xaml.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Win32.SafeHandles;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureTool.UI.Windows.Xaml.Windows;

internal sealed partial class CaptureOverlayHost : IDisposable
{
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
    private HWND? _borderHwnd;
    private DesktopWindowXamlSource? _xamlSource;
    private DesktopWindowXamlSource? _borderXamlSource;
    private CaptureOverlayView? _overlayView;
    private CaptureOverlayBorder? _borderControl;

    private static (HWND hwnd, DesktopWindowXamlSource xamlSource, CaptureOverlayView view) CreateCaptureOverlayWindow(MonitorCaptureResult monitor, Rectangle area)
    {
        unsafe
        {
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

            HWND hwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
                className,
                null,
                WINDOW_STYLE.WS_VISIBLE | WINDOW_STYLE.WS_POPUP,
                monitor.MonitorBounds.X, // x
                monitor.MonitorBounds.Y + 14, // y + padding
                (int)(408 * monitor.Scale), // w
                (int)(76 * monitor.Scale), //h
                new(IntPtr.Zero),
                null,
                new DestroyIconSafeHandle(wndClass.hInstance),
                null);

            PInvoke.SetWindowDisplayAffinity(hwnd, WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);

            DesktopWindowXamlSource xamlSource = new();
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            xamlSource.Initialize(windowId);
            
            CaptureOverlayView view = new(monitor, area);
            xamlSource.Content = view;

            Win32WindowHelpers.HorizontalCenterOnScreen(hwnd);

            return (hwnd, xamlSource, view);
        }
    }

    private static (HWND hwnd, DesktopWindowXamlSource xamlSource, CaptureOverlayBorder border) CreateCaptureOverlayBorderWindow(MonitorCaptureResult monitor, Rectangle area)
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

            HWND borderHwnd = PInvoke.CreateWindowEx(
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

            PInvoke.SetWindowDisplayAffinity(borderHwnd, WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);

            DesktopWindowXamlSource xamlSource = new();
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(borderHwnd);
            xamlSource.Initialize(windowId);
            
            CaptureOverlayBorder border = new();
            xamlSource.Content = border;

            return (borderHwnd, xamlSource, border);
        }
    }

    public void Initialize(NewCaptureArgs args)
    {
        if (_hwnd != null && _borderHwnd != null)
        {
            return;
        }

        var monitor = args.Monitor;
        var area = args.Area;
        
        var overlayResult = CreateCaptureOverlayWindow(monitor, area);
        _hwnd = overlayResult.hwnd;
        _xamlSource = overlayResult.xamlSource;
        _overlayView = overlayResult.view;
        
        var borderResult = CreateCaptureOverlayBorderWindow(monitor, area);
        _borderHwnd = borderResult.hwnd;
        _borderXamlSource = borderResult.xamlSource;
        _borderControl = borderResult.border;
    }

    public void Activate()
    {
        if (_hwnd != null && _borderHwnd != null)
        {
            Win32WindowHelpers.SetActiveWindow(_borderHwnd.Value);
            Win32WindowHelpers.SetActiveWindow(_hwnd.Value);
            Win32WindowHelpers.SetForegroundWindow(_hwnd.Value);
        }
    }

    public void Close()
    {
        DestroyBorderWindow();
        DestroyOverlayWindow();
    }

    public void HideBorder()
    {
        DestroyBorderWindow();
    }

    private void DestroyOverlayWindow()
    {
        if (_overlayView != null)
        {
            try
            {
                _overlayView.ViewModel?.Dispose();
            }
            catch { }
            _overlayView = null;
        }

        if (_xamlSource != null)
        {
            try
            {
                _xamlSource.Content = null;
                (_xamlSource as IDisposable)?.Dispose();
                _xamlSource = null;
            }
            catch { }
        }

        if (_hwnd != null)
        {
            try
            {
                PInvoke.DestroyWindow(_hwnd.Value);
            }
            catch { }
            _hwnd = null;
        }
    }

    private void DestroyBorderWindow()
    {
        if (_borderControl != null)
        {
            _borderControl = null;
        }

        if (_borderXamlSource != null)
        {
            try
            {
                _borderXamlSource.Content = null;
                (_borderXamlSource as IDisposable)?.Dispose();
                _borderXamlSource = null;
            }
            catch { }
        }

        if (_borderHwnd != null)
        {
            try
            {
                PInvoke.DestroyWindow(_borderHwnd.Value);
            }
            catch { }
            _borderHwnd = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static LRESULT BorderWindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }
}
