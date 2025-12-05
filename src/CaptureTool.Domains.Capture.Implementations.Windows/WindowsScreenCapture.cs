using CaptureTool.Domains.Capture.Interfaces;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

public class WindowsScreenCapture : IScreenCapture
{
    public Bitmap CombineMonitors(IList<MonitorCaptureResult> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor must be provided.", nameof(monitors));
        }

        // Step 1: Calculate the union of all monitor bounds
        Rectangle unionBounds = monitors[0].MonitorBounds;
        foreach (var m in monitors)
            unionBounds = Rectangle.Union(unionBounds, m.MonitorBounds);

        int finalWidth = unionBounds.Width;
        int finalHeight = unionBounds.Height;
        byte[] finalBuffer = new byte[finalWidth * finalHeight * 4]; // BGRA

        // Step 2: Copy each monitor into final buffer
        foreach (var monitor in monitors)
        {
            var src = monitor.PixelBuffer;
            var bounds = monitor.MonitorBounds;
            int width = bounds.Width;
            int height = bounds.Height;

            int offsetX = bounds.X - unionBounds.X;
            int offsetY = bounds.Y - unionBounds.Y;

            for (int y = 0; y < height; y++)
            {
                int srcRowStart = y * width * 4;
                int dstRowStart = ((offsetY + y) * finalWidth + offsetX) * 4;

                Buffer.BlockCopy(src, srcRowStart, finalBuffer, dstRowStart, width * 4);
            }
        }

        // Step 3: Create System.Drawing.Bitmap
        Bitmap bmp = new(finalWidth, finalHeight, PixelFormat.Format32bppArgb);
        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, finalWidth, finalHeight),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb
        );
        Marshal.Copy(finalBuffer, 0, bmpData.Scan0, finalBuffer.Length);
        bmp.UnlockBits(bmpData);
        return bmp;
    }

    public unsafe MonitorCaptureResult[] CaptureAllMonitors()
    {
        var results = new List<MonitorCaptureResult>();

        PInvoke.EnumDisplayMonitors(
            hdc: HDC.Null,
            lprcClip: null,
            lpfnEnum: new MONITORENUMPROC(EnumMonitorCallback),
            dwData: new()
        );

        return [.. results];

        unsafe BOOL EnumMonitorCallback(
            HMONITOR hMonitor,
            HDC hdcMonitor,
            RECT* lprcMonitor,
            LPARAM lParam)
        {
            var mi = new MONITORINFOEXW();
            mi.monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>();
            if (!PInvoke.GetMonitorInfo(hMonitor, ref mi.monitorInfo))
                return true;

            int width = mi.monitorInfo.rcMonitor.right - mi.monitorInfo.rcMonitor.left;
            int height = mi.monitorInfo.rcMonitor.bottom - mi.monitorInfo.rcMonitor.top;

            // Get DPI
            PInvoke.GetDpiForMonitor(
                hmonitor: hMonitor,
                dpiType: global::Windows.Win32.UI.HiDpi.MONITOR_DPI_TYPE.MDT_DEFAULT,
                dpiX: out uint dpiX,
                dpiY: out uint dpiY
            );

            var isPrimary = mi.monitorInfo.dwFlags == 1;// MONITORINFOF_PRIMARY;

            // Initialize handles
            HDC hdcScreen = default;
            HDC hdcMem = default;
            HBITMAP hBmp = default;
            HGDIOBJ oldBmp = default;

            byte[] pixels = new byte[width * height * 4];

            try
            {
                hdcScreen = PInvoke.GetDC(HWND.Null);
                if (hdcScreen.Value == null)
                    return true;

                hdcMem = PInvoke.CreateCompatibleDC(hdcScreen);
                if (hdcMem.Value == null)
                    return true;

                hBmp = PInvoke.CreateCompatibleBitmap(hdcScreen, width, height);
                if (hBmp.Value == null)
                    return true;

                oldBmp = PInvoke.SelectObject(hdcMem, hBmp);

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

                if (bltOk)
                {
                    pixels = GetBitmapBytes(hdcMem, hBmp, width, height);
                }
            }
            finally
            {
                if (oldBmp.Value != null && hdcMem.Value != null)
                    PInvoke.SelectObject(hdcMem, oldBmp);

                if (hBmp.Value != null)
                    PInvoke.DeleteObject(hBmp);

                if (hdcMem.Value != null)
                    PInvoke.DeleteDC(hdcMem);

                if (hdcScreen.Value != null)
                    _ = PInvoke.ReleaseDC(HWND.Null, hdcScreen);
            }

            int workW = mi.monitorInfo.rcWork.right - mi.monitorInfo.rcWork.left;
            int workH = mi.monitorInfo.rcWork.bottom - mi.monitorInfo.rcWork.top;

            results.Add(new MonitorCaptureResult(
                hMonitor,
                pixels,
                dpiX,
                new Rectangle(
                    mi.monitorInfo.rcMonitor.left,
                    mi.monitorInfo.rcMonitor.top,
                    width,
                    height),
                new Rectangle(
                    mi.monitorInfo.rcWork.left,
                    mi.monitorInfo.rcWork.top,
                    workW,
                    workH),
                isPrimary
            ));

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

    public void SaveImageToFile(Image image, string filePath)
    {
        image.Save(filePath, ImageFormat.Png);
    }

    public Bitmap CreateBitmapFromMonitorCaptureResult(MonitorCaptureResult monitor)
    {
        Rectangle monitorBounds = monitor.MonitorBounds;

        // Create a bitmap for the full monitor
        Bitmap fullBmp = new(monitorBounds.Width, monitorBounds.Height, PixelFormat.Format32bppArgb);
        BitmapData bmpData = fullBmp.LockBits(
            new Rectangle(0, 0, monitorBounds.Width, monitorBounds.Height),
            ImageLockMode.WriteOnly,
            fullBmp.PixelFormat
        );

        try
        {
            Marshal.Copy(monitor.PixelBuffer, 0, bmpData.Scan0, monitor.PixelBuffer.Length);
        }
        finally
        {
            fullBmp.UnlockBits(bmpData);
        }

        return fullBmp;
    }

    public Bitmap CreateCroppedBitmap(Bitmap image, Rectangle area, float scale)
    {
        // Crop to the selected area
        int cropX = (int)Math.Round((area.Left) * scale);
        int cropY = (int)Math.Round((area.Top) * scale);
        int cropWidth = (int)Math.Round(area.Width * scale);
        int cropHeight = (int)Math.Round(area.Height * scale);

        // Ensure cropping stays within image bounds
        cropX = Math.Clamp(cropX, 0, image.Width - 1);
        cropY = Math.Clamp(cropY, 0, image.Height - 1);
        cropWidth = Math.Clamp(cropWidth, 1, image.Width - cropX);
        cropHeight = Math.Clamp(cropHeight, 1, image.Height - cropY);

        var cropRect = new Rectangle(cropX, cropY, cropWidth, cropHeight);
        var croppedBmp = image.Clone(cropRect, image.PixelFormat);
        return croppedBmp;
    }
}
