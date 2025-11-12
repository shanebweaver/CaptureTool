using Microsoft.UI.Xaml;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureTool.UI.Windows.Utils;

public static partial class Win32WindowHelpers
{
    private static readonly HWND HWND_TOP = new(0);
    private static readonly HWND HWND_BOTTOM = new(1);
    private static readonly HWND HWND_TOPMOST = new(-1);
    private static readonly HWND HWND_NOTOPMOST = new(-2);

    public static nint GetWindowHandle(Window window)
    {
        return WinRT.Interop.WindowNative.GetWindowHandle(window);
    }

    public static void SetForegroundWindow(nint windowHandle)
    {
        PInvoke.SetForegroundWindow(new(windowHandle));
    }

    public static void ExcludeFromScreenCapture(nint windowHandle)
    {
        PInvoke.SetWindowDisplayAffinity(new(windowHandle), WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);
    }

    public static void IncludeInScreenCapture(nint windowHandle)
    {
        PInvoke.SetWindowDisplayAffinity(new(windowHandle), WINDOW_DISPLAY_AFFINITY.WDA_NONE);
    }

    public static void MakeBorderlessOverlay(nint windowHandle)
    {
        HWND hwnd = new(windowHandle);

        // Remove standard window chrome (caption, borders, sysmenu)
        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)(
            WINDOW_STYLE.WS_CAPTION |
            WINDOW_STYLE.WS_THICKFRAME |
            WINDOW_STYLE.WS_SYSMENU |
            WINDOW_STYLE.WS_MINIMIZEBOX |
            WINDOW_STYLE.WS_MAXIMIZEBOX);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);

        // Hide from taskbar and Alt+Tab
        var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        exStyle &= ~(int)WINDOW_EX_STYLE.WS_EX_APPWINDOW;
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);

        // Force the system to re-apply new styles
        PInvoke.SetWindowPos(
            hwnd,
            HWND_TOPMOST,
            0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);

        // Optional: maximize to current monitor bounds
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_MAXIMIZE);
    }

    public static void MoveAndResize(nint windowHandle, Rectangle bounds)
    {
        PInvoke.SetWindowPos(
            new HWND(windowHandle),
            new HWND(0),
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
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

    public static void Restore(nint hwnd)
    {
        PInvoke.ShowWindow(new HWND(hwnd), SHOW_WINDOW_CMD.SW_RESTORE);
    }

    public static void SetActiveWindow(nint hwnd)
    {
        PInvoke.SetActiveWindow(new(hwnd));
    }
}
