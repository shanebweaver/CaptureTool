using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;

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

    /// <summary>
    /// Centers the window on the current monitor
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <param name="width">Width of the window in device independent pixels, or <c>null</c> if keeping the current size</param>
    /// <param name="height">Height of the window in device independent pixels, or <c>null</c> if keeping the current size</param>
    public static void CenterOnScreen(IntPtr hwnd, double? width = null, double? height = null)
    {
        var hwndDesktop = global::Windows.Win32.PInvoke.MonitorFromWindow(
            new global::Windows.Win32.Foundation.HWND(hwnd), 
            global::Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        global::Windows.Win32.Graphics.Gdi.MONITORINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<global::Windows.Win32.Graphics.Gdi.MONITORINFO>()
        };
        global::Windows.Win32.PInvoke.GetMonitorInfo(hwndDesktop, ref info);
        var dpi = global::Windows.Win32.PInvoke.GetDpiForWindow(new(hwnd));
        global::Windows.Win32.PInvoke.GetWindowRect(new(hwnd), out global::Windows.Win32.Foundation.RECT windowRect);
        var scalingFactor = dpi / 96d;
        var w = width.HasValue ? (int)(width * scalingFactor) : windowRect.right - windowRect.left;
        var h = height.HasValue ? (int)(height * scalingFactor) : windowRect.bottom - windowRect.top;
        var cx = (info.rcMonitor.left + info.rcMonitor.right) / 2;
        var cy = (info.rcMonitor.bottom + info.rcMonitor.top) / 2;
        var left = cx - (w / 2);
        var top = cy - (h / 2);
        SetWindowPosOrThrow(new global::Windows.Win32.Foundation.HWND(hwnd), new global::Windows.Win32.Foundation.HWND(), left, top, w, h, 0);
    }

    public static void HorizontalCenterOnScreen(IntPtr hwnd, double? width = null, double? height = null)
    {
        var hwndDesktop = global::Windows.Win32.PInvoke.MonitorFromWindow(
            new global::Windows.Win32.Foundation.HWND(hwnd),
            global::Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        global::Windows.Win32.Graphics.Gdi.MONITORINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<global::Windows.Win32.Graphics.Gdi.MONITORINFO>()
        };
        global::Windows.Win32.PInvoke.GetMonitorInfo(hwndDesktop, ref info);
        var dpi = global::Windows.Win32.PInvoke.GetDpiForWindow(new(hwnd));
        global::Windows.Win32.PInvoke.GetWindowRect(new(hwnd), out global::Windows.Win32.Foundation.RECT windowRect);
        var scalingFactor = dpi / 96d;
        var w = width.HasValue ? (int)(width * scalingFactor) : windowRect.right - windowRect.left;
        var h = height.HasValue ? (int)(height * scalingFactor) : windowRect.bottom - windowRect.top;
        var cx = (info.rcMonitor.left + info.rcMonitor.right) / 2;
        var left = cx - (w / 2);
        var top = windowRect.top;
        SetWindowPosOrThrow(new global::Windows.Win32.Foundation.HWND(hwnd), new global::Windows.Win32.Foundation.HWND(), left, top, w, h, 0);
    }

    private static void SetWindowPosOrThrow(global::Windows.Win32.Foundation.HWND hWnd, global::Windows.Win32.Foundation.HWND hWndInsertAfter, int X, int Y, int cx, int cy, global::Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS uFlags)
    {
        bool result = global::Windows.Win32.PInvoke.SetWindowPos(hWnd, hWndInsertAfter, X, Y, cx, cy, uFlags);
        if (!result)
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
    }
}
