using CaptureTool.Capture;
using CaptureTool.UI.Windows.Xaml.Controls;
using CaptureTool.UI.Windows.Xaml.Extensions;
using CaptureTool.UI.Windows.Xaml.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Win32.SafeHandles;
using System;
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
        public DestroyIconSafeHandle(HINSTANCE hINSTANCE) : base(true) {
            handle = hINSTANCE;
        }

        protected override bool ReleaseHandle()
        {
            return PInvoke.DestroyIcon(new(handle));
        }
    }

    private HWND? _hwnd;
    private HWND? _borderHwnd;

    private static HWND CreateCaptureOverlayWindow(MonitorCaptureResult monitor, Rectangle area)
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
            xamlSource.Content = new CaptureOverlayView(monitor, area);

            WindowExtensions.HorizontalCenterOnScreen(hwnd);

            return hwnd;
        }
    }

    private static HWND CreateCaptureOverlayBorderWindow(MonitorCaptureResult monitor, Rectangle area)
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
            xamlSource.Content = new CaptureOverlayBorder();

            return borderHwnd;
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
        _hwnd = CreateCaptureOverlayWindow(monitor, area);
        _borderHwnd = CreateCaptureOverlayBorderWindow(monitor, area);
    }

    public void Activate()
    {
        if (_hwnd != null && _borderHwnd != null)
        {
            PInvoke.SetActiveWindow(_borderHwnd.Value);
            PInvoke.SetActiveWindow(_hwnd.Value);
            PInvoke.SetForegroundWindow(_hwnd.Value);
        }
    }

    public void Close()
    {
        DestroyBorderWindow();
        DestroyOverlayWindow();
        GC.Collect();
    }

    public void HideBorder()
    {
        DestroyBorderWindow();
    }

    private void DestroyOverlayWindow()
    {
        if (_hwnd != null)
        {
            PInvoke.DestroyWindow(_hwnd.Value);
            _hwnd = null;
        }
    }

    private void DestroyBorderWindow()
    {
        if (_borderHwnd != null)
        {
            PInvoke.DestroyWindow(_borderHwnd.Value);
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
        if (_hwnd != null)
        {
            PInvoke.DestroyWindow(_hwnd.Value);
            _hwnd = null;
        }
        if (_borderHwnd != null)
        {
            PInvoke.DestroyWindow(_borderHwnd.Value);
            _borderHwnd = null;
        }
    }
}
