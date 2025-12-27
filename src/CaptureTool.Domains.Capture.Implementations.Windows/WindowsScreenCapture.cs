using CaptureTool.Domains.Capture.Interfaces;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

public class WindowsScreenCapture : IScreenCapture
{
    public Bitmap CombineMonitors(IList<MonitorCaptureResult> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor must be provided.", nameof(monitors));
        }

        // Calculate the union of all monitor bounds
        Rectangle unionBounds = monitors[0].MonitorBounds;
        foreach (var m in monitors)
            unionBounds = Rectangle.Union(unionBounds, m.MonitorBounds);

        int finalWidth = unionBounds.Width;
        int finalHeight = unionBounds.Height;
        byte[] finalBuffer = new byte[finalWidth * finalHeight * 4]; // BGRA

        // Copy each monitor into final buffer
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

        // Create System.Drawing.Bitmap
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

        // Use EnumDisplayMonitors to enumerate all monitors
        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr lParam) =>
            {
                // Capture this monitor using C++ implementation
                IntPtr screenshotHandle = CaptureInterop.CaptureMonitorScreenshot(hMonitor);
                if (screenshotHandle == IntPtr.Zero)
                {
                    // Continue enumeration even if one monitor fails - we want to capture as many as possible
                    return true;
                }

                try
                {
                    // Get screenshot info
                    CaptureInterop.GetScreenshotInfo(
                        screenshotHandle,
                        out int width,
                        out int height,
                        out int left,
                        out int top,
                        out uint dpiX,
                        out uint dpiY,
                        out bool isPrimary);

                    // Allocate buffer and copy pixels
                    byte[] pixels = new byte[width * height * 4];
                    if (!CaptureInterop.CopyScreenshotPixels(screenshotHandle, pixels, pixels.Length))
                    {
                        // Continue enumeration even if pixel copy fails - we want to capture as many as possible
                        return true;
                    }

                    // Get monitor info for work area
                    MONITORINFOEX monitorInfo = new();
                    monitorInfo.cbSize = Marshal.SizeOf<MONITORINFOEX>();
                    if (!GetMonitorInfo(hMonitor, ref monitorInfo))
                    {
                        return true;
                    }

                    int workW = monitorInfo.rcWork.right - monitorInfo.rcWork.left;
                    int workH = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;

                    results.Add(new MonitorCaptureResult(
                        hMonitor,
                        pixels,
                        dpiX,
                        new Rectangle(left, top, width, height),
                        new Rectangle(
                            monitorInfo.rcWork.left,
                            monitorInfo.rcWork.top,
                            workW,
                            workH),
                        isPrimary
                    ));
                }
                finally
                {
                    CaptureInterop.FreeScreenshot(screenshotHandle);
                }

                return true;
            },
            IntPtr.Zero);

        return [.. results];
    }

    // P/Invoke declarations for EnumDisplayMonitors
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        ref RECT lprcMonitor,
        IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

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
