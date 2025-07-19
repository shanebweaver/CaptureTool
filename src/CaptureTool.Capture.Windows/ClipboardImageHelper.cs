using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CaptureTool.Capture.Windows;

public static class ClipboardImageHelper
{
    public static async void CombineMonitorsAndCopyToClipboard(IList<MonitorCaptureResult> monitors)
    {
        if (monitors.Count == 0)
            return;

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

                System.Buffer.BlockCopy(src, srcRowStart, finalBuffer, dstRowStart, width * 4);
            }
        }

        // Step 3: Create System.Drawing.Bitmap
        using var bmp = new Bitmap(finalWidth, finalHeight, PixelFormat.Format32bppArgb);
        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, finalWidth, finalHeight),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb
        );
        Marshal.Copy(finalBuffer, 0, bmpData.Scan0, finalBuffer.Length);
        bmp.UnlockBits(bmpData);

        // Step 4: Convert to stream and copy to WinUI clipboard
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);

        var randomAccessStream = new InMemoryRandomAccessStream();
        using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
        {
            await RandomAccessStream.CopyAsync(ms.AsInputStream(), outputStream);
            await outputStream.FlushAsync();
        }

        var dataPackage = new DataPackage();
        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(randomAccessStream));
        Clipboard.SetContent(dataPackage);
    }
}
