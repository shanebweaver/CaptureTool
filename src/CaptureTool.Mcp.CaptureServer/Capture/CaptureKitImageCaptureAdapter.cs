using CaptureKit.Abstractions;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Mcp.CaptureServer.Models;
using System.Drawing;
using System.Drawing.Imaging;

namespace CaptureTool.Mcp.CaptureServer.Capture;

public sealed class CaptureKitImageCaptureAdapter : ICaptureKitImageCaptureAdapter
{
    private readonly IImageCaptureService _imageCaptureService;
    private readonly IMcpCaptureStore _captureStore;
    private readonly TimeProvider _timeProvider;

    public CaptureKitImageCaptureAdapter(
        IImageCaptureService imageCaptureService,
        IMcpCaptureStore captureStore,
        TimeProvider timeProvider)
    {
        _imageCaptureService = imageCaptureService;
        _captureStore = captureStore;
        _timeProvider = timeProvider;
    }

    public McpCapture Capture(
        CaptureTarget target,
        Rectangle sourceBounds,
        string sourceKind,
        uint dpi,
        float scale,
        string? targetId = null,
        string? targetTitle = null,
        Rectangle? monitorBounds = null,
        Rectangle? workAreaBounds = null,
        bool? isPrimary = null)
    {
        string outputPath = Path.Combine(
            Path.GetTempPath(),
            "CaptureTool",
            "McpCapture",
            $"{Guid.NewGuid():N}.png");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            ImageCaptureResult result = _imageCaptureService.Capture(new ImageCaptureRequest(target, outputPath));
            byte[] pngBytes = File.ReadAllBytes(result.FilePath);
            Size imageSize = new(result.Width, result.Height);

            if (target.Kind == CaptureTargetKind.Window)
            {
                (pngBytes, imageSize) = NormalizeWindowCapture(pngBytes, imageSize, sourceBounds.Size);
            }

            var metadata = McpCaptureMetadata.Create(
                McpCaptureIds.Create(),
                _timeProvider.GetUtcNow(),
                imageSize.Width,
                imageSize.Height,
                dpi,
                scale,
                sourceBounds,
                sourceKind,
                "png",
                monitorBounds,
                workAreaBounds,
                isPrimary,
                targetId,
                targetTitle);

            var capture = new McpCapture(pngBytes, metadata);
            _captureStore.Store(capture);
            return capture;
        }
        finally
        {
            TryDeleteFile(outputPath);
        }
    }

    private static (byte[] PngBytes, Size ImageSize) NormalizeWindowCapture(byte[] pngBytes, Size reportedSize, Size expectedContentSize)
    {
        if (expectedContentSize.Width <= 0
            || expectedContentSize.Height <= 0
            || (reportedSize.Width <= expectedContentSize.Width && reportedSize.Height <= expectedContentSize.Height))
        {
            return (pngBytes, reportedSize);
        }

        using var inputStream = new MemoryStream(pngBytes);
        using var bitmap = new Bitmap(inputStream);
        int cropWidth = Math.Min(expectedContentSize.Width, bitmap.Width);
        int cropHeight = Math.Min(expectedContentSize.Height, bitmap.Height);

        if (!HasBlankRightOrBottomPadding(bitmap, cropWidth, cropHeight))
        {
            return (pngBytes, new Size(bitmap.Width, bitmap.Height));
        }

        using var cropped = new Bitmap(cropWidth, cropHeight, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(cropped))
        {
            graphics.DrawImage(
                bitmap,
                new Rectangle(0, 0, cropWidth, cropHeight),
                new Rectangle(0, 0, cropWidth, cropHeight),
                GraphicsUnit.Pixel);
        }

        using var outputStream = new MemoryStream();
        cropped.Save(outputStream, ImageFormat.Png);
        return (outputStream.ToArray(), new Size(cropWidth, cropHeight));
    }

    private static bool HasBlankRightOrBottomPadding(Bitmap bitmap, int contentWidth, int contentHeight)
    {
        if (contentWidth >= bitmap.Width && contentHeight >= bitmap.Height)
        {
            return false;
        }

        int stride = Math.Max(1, Math.Max(bitmap.Width, bitmap.Height) / 400);
        return HasBlankPadding(bitmap, new Rectangle(contentWidth, 0, bitmap.Width - contentWidth, bitmap.Height), stride)
            || HasBlankPadding(bitmap, new Rectangle(0, contentHeight, bitmap.Width, bitmap.Height - contentHeight), stride);
    }

    private static bool HasBlankPadding(Bitmap bitmap, Rectangle paddingBounds, int stride)
    {
        if (paddingBounds.Width <= 0 || paddingBounds.Height <= 0)
        {
            return false;
        }

        int totalSamples = 0;
        int blankSamples = 0;

        for (int y = paddingBounds.Top; y < paddingBounds.Bottom; y += stride)
        {
            for (int x = paddingBounds.Left; x < paddingBounds.Right; x += stride)
            {
                totalSamples++;
                if (IsBlankPaddingPixel(bitmap.GetPixel(x, y)))
                {
                    blankSamples++;
                }
            }
        }

        return totalSamples > 0 && blankSamples / (double)totalSamples >= 0.995d;
    }

    private static bool IsBlankPaddingPixel(Color color)
        => color.A == 0 || (color.R <= 3 && color.G <= 3 && color.B <= 3);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
