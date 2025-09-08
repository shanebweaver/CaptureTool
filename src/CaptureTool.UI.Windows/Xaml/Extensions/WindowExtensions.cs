using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureTool.UI.Windows.Xaml.Extensions;

internal static partial class WindowExtensions
{
    public static void Restore(this Window window)
    {
        window.UpdateOverlappedPresenter(p => p.Restore());
    }

    private static void UpdateOverlappedPresenter(this Window window, Action<OverlappedPresenter> action)
    {
        ArgumentNullException.ThrowIfNull(window);

        var appwindow = window.AppWindow;
        if (appwindow.Presenter is OverlappedPresenter overlapped)
        {
            action(overlapped);
        }
        else
        {
            throw new NotSupportedException($"Not supported with a {appwindow.Presenter.Kind} presenter");
        }
    }

    public static void CenterOnScreen(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        CenterOnScreen(hwnd);
    }

    public static void CenterOnScreen(IntPtr hwndPtr, double? width = null, double? height = null)
    {
        HWND hwnd = new(hwndPtr);
        HMONITOR hwndDesktop = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MONITORINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };
        PInvoke.GetMonitorInfo(hwndDesktop, ref info);

        uint dpi = PInvoke.GetDpiForWindow(hwnd);
        PInvoke.GetWindowRect(hwnd, out RECT windowRect);
        
        double scalingFactor = dpi / 96d;
        double w = width.HasValue ? (int)(width * scalingFactor) : windowRect.right - windowRect.left;
        double h = height.HasValue ? (int)(height * scalingFactor) : windowRect.bottom - windowRect.top;
        double cx = (info.rcMonitor.left + info.rcMonitor.right) / 2;
        double cy = (info.rcMonitor.bottom + info.rcMonitor.top) / 2;
        double left = cx - (w / 2);
        double top = cy - (h / 2);
        SetWindowPosOrThrow(hwnd, new HWND(), (int)left, (int)top, (int)w, (int)h, 0);
    }

    public static void HorizontalCenterOnScreen(IntPtr hwndPtr, double? width = null, double? height = null)
    {
        HWND hwnd = new(hwndPtr);
        HMONITOR hwndDesktop = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MONITORINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };
        PInvoke.GetMonitorInfo(hwndDesktop, ref info);

        uint dpi = PInvoke.GetDpiForWindow(hwnd);
        PInvoke.GetWindowRect(hwnd, out RECT windowRect);

        double scalingFactor = dpi / 96d;
        double w = width.HasValue ? (int)(width * scalingFactor) : windowRect.right - windowRect.left;
        double h = height.HasValue ? (int)(height * scalingFactor) : windowRect.bottom - windowRect.top;
        double cx = (info.rcMonitor.left + info.rcMonitor.right) / 2;
        double left = cx - (w / 2);
        int top = windowRect.top;
        SetWindowPosOrThrow(hwnd, new HWND(), (int)left, top, (int)w, (int)h, 0);
    }

    private static void SetWindowPosOrThrow(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags)
    {
        bool result = PInvoke.SetWindowPos(hWnd, hWndInsertAfter, X, Y, cx, cy, uFlags);
        if (!result)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
        }
    }
}
