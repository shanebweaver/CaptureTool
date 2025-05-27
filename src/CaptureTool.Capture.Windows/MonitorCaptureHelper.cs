using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CaptureTool.Capture.Windows;

public static class MonitorCaptureHelper
{
    // Win32 API imports and structs
    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public uint[] bmiColors;
    }

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr hdc,
        IntPtr hbmp,
        uint uStartScan,
        uint cScanLines,
        [Out] byte[] lpvBits,
        [In, Out] ref BITMAPINFO lpbi,
        uint uUsage);

    private const int BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;
    private const int SRCCOPY = 0x00CC0020;

    public static List<MonitorCaptureResult> CaptureAllMonitors()
    {
        var results = new List<MonitorCaptureResult>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData) =>
        {
            MonitorInfoEx mi = new()
            {
                cbSize = Marshal.SizeOf<MonitorInfoEx>()
            };
            if (!GetMonitorInfo(hMonitor, ref mi))
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"GetMonitorInfo failed: {error}");
                return true;
            }

            int width = mi.rcMonitor.right - mi.rcMonitor.left;
            int height = mi.rcMonitor.bottom - mi.rcMonitor.top;

            IntPtr hdcScreen = GetDC(IntPtr.Zero);
            if (hdcScreen == IntPtr.Zero)
                return true;

            IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
            if (hdcMem == IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, hdcScreen);
                return true;
            }

            IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
            if (hBitmap == IntPtr.Zero)
            {
                DeleteDC(hdcMem);
                ReleaseDC(IntPtr.Zero, hdcScreen);
                return true;
            }

            IntPtr hOld = SelectObject(hdcMem, hBitmap);

            bool bltResult = BitBlt(hdcMem, 0, 0, width, height, hdcScreen, mi.rcMonitor.left, mi.rcMonitor.top, SRCCOPY);
            byte[] pixels;
            if (bltResult)
            {
                pixels = GetBitmapBytes(hdcMem, hBitmap, width, height);
            }
            else
            {
                pixels = new byte[width * height * 4];
            }

            results.Add(new MonitorCaptureResult
            {
                Width = width,
                Height = height,
                PixelBuffer = pixels,
                Left = mi.rcMonitor.left,
                Top = mi.rcMonitor.top
            });

            SelectObject(hdcMem, hOld);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);

            return true;
        }, IntPtr.Zero);

        return results;
    }

    private static byte[] GetBitmapBytes(IntPtr hdc, IntPtr hBitmap, int width, int height)
    {
        // BGRA 32bpp
        BITMAPINFO bmi = new BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
        bmi.bmiHeader.biWidth = width;
        bmi.bmiHeader.biHeight = -height; // top-down DIB
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = BI_RGB;
        bmi.bmiHeader.biSizeImage = (uint)(width * height * 4);
        bmi.bmiColors = new uint[256]; // Initialize to avoid NullReferenceException

        byte[] pixels = new byte[width * height * 4];
        int scanLines = GetDIBits(hdc, hBitmap, 0, (uint)height, pixels, ref bmi, DIB_RGB_COLORS);
        if (scanLines == 0)
        {
            // Failed to get bits, return empty array
            return new byte[width * height * 4];
        }
        return pixels;
    }
}