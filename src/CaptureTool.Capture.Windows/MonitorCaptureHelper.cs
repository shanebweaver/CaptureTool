using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace CaptureTool.Capture.Windows;

public static partial class MonitorCaptureHelper
{
    public static unsafe List<MonitorCaptureResult> CaptureAllMonitors()
    {
        var results = new List<MonitorCaptureResult>();

        // Enumerate all monitors
        PInvoke.EnumDisplayMonitors(
            hdc: HDC.Null,
            lprcClip: null,
            lpfnEnum: new MONITORENUMPROC(EnumMonitorCallback),
            dwData: new()
        );

        return results;

        // Local callback
        unsafe BOOL EnumMonitorCallback(
            HMONITOR hMonitor,
            HDC hdcMonitor,
            RECT* lprcMonitor,
            LPARAM lParam)
        {
            // Get monitor info
            var mi = new MONITORINFOEXW();
            mi.monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>();
            if (!PInvoke.GetMonitorInfo(hMonitor, ref mi.monitorInfo))
            {
                return true; // continue
            }

            int width = mi.monitorInfo.rcMonitor.right - mi.monitorInfo.rcMonitor.left;
            int height = mi.monitorInfo.rcMonitor.bottom - mi.monitorInfo.rcMonitor.top;

            // DPI
            PInvoke.GetDpiForMonitor(
                hmonitor: hMonitor,
                dpiType: global::Windows.Win32.UI.HiDpi.MONITOR_DPI_TYPE.MDT_DEFAULT,
                dpiX: out uint dpiX,
                dpiY: out uint dpiY
            );

            // Get screen DC
            HDC hdcScreen = PInvoke.GetDC(HWND.Null);
            if (hdcScreen == IntPtr.Zero)
                return true;

            // Create compatible DC
            HDC hdcMem = PInvoke.CreateCompatibleDC(hdcScreen);
            if (hdcMem == IntPtr.Zero)
            {
                _ = PInvoke.ReleaseDC(HWND.Null, hdcScreen);
                return true;
            }

            // Create bitmap
            HBITMAP hBmp = PInvoke.CreateCompatibleBitmap(hdcScreen, width, height);
            if (hBmp == IntPtr.Zero)
            {
                PInvoke.DeleteDC(hdcMem);
                _ = PInvoke.ReleaseDC(HWND.Null, hdcScreen);
                return true;
            }

            // Select and blit
            HGDIOBJ oldBmp = PInvoke.SelectObject(hdcMem, hBmp);
            BOOL bltOk = PInvoke.BitBlt(
                hdc: hdcMem,
                x: 0,
                y: 0,
                cx: width,
                cy: height,
                hdcSrc: hdcScreen,
                x1: mi.monitorInfo.rcMonitor.left,
                y1: mi.monitorInfo.rcMonitor.top,
                rop: ROP_CODE.SRCCOPY
            );

            byte[] pixels = bltOk
                ? GetBitmapBytes(hdcMem, hBmp, width, height)
                : new byte[width * height * 4];

            // Work area
            int workW = mi.monitorInfo.rcWork.right - mi.monitorInfo.rcWork.left;
            int workH = mi.monitorInfo.rcWork.bottom - mi.monitorInfo.rcWork.top;

            // Add result
            results.Add(new MonitorCaptureResult(
                hMonitor,
                pixels,
                dpiX,
                new System.Drawing.Rectangle(mi.monitorInfo.rcMonitor.left, mi.monitorInfo.rcMonitor.top, width, height),
                new System.Drawing.Rectangle(mi.monitorInfo.rcWork.left, mi.monitorInfo.rcWork.top, workW, workH)
            ));

            // Cleanup
            PInvoke.SelectObject(hdcMem, oldBmp);
            PInvoke.DeleteObject(hBmp);
            PInvoke.DeleteDC(hdcMem);
            _ = PInvoke.ReleaseDC(HWND.Null, hdcScreen);

            return true;
        }
    }

    private static unsafe byte[] GetBitmapBytes(HDC hdc, HBITMAP hBitmap, int width, int height)
    {
        // Prepare BITMAPINFO (32bpp top-down)
        BITMAPINFO bmi = default;
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
        bmi.bmiHeader.biWidth = width;
        bmi.bmiHeader.biHeight = -height; // negative = top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0;
        bmi.bmiHeader.biSizeImage = (uint)(width * height * 4);

        byte[] pixels = new byte[width * height * 4];

        // Pin managed array for pointer-based GetDIBits
        fixed (byte* pPixels = pixels)
        {
            // BITMAPINFO is a stack variable, so we can take its address directly
            BITMAPINFO* pBmi = &bmi;
            int lines = PInvoke.GetDIBits(
                hdc,
                hBitmap,
                0u,
                (uint)height,
                pPixels,
                pBmi,
                0
            );
            // lines == number of scan lines copied
        }

        return pixels;
    }

}