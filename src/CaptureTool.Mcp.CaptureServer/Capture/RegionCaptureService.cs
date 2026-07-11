using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
using CaptureKit.Abstractions;
using CaptureTool.Domain.Capture;
using System.Drawing;
using System.Drawing.Imaging;

namespace CaptureTool.Mcp.CaptureServer.Capture;

public sealed class RegionCaptureService : IRegionCaptureService
{
    private readonly IScreenCapture _screenCapture;
    private readonly ICaptureKitImageCaptureAdapter _imageCaptureAdapter;
    private readonly IMcpCaptureStore _captureStore;
    private readonly TimeProvider _timeProvider;

    public RegionCaptureService(
        IScreenCapture screenCapture,
        ICaptureKitImageCaptureAdapter imageCaptureAdapter,
        IMcpCaptureStore captureStore,
        TimeProvider timeProvider)
    {
        _screenCapture = screenCapture;
        _imageCaptureAdapter = imageCaptureAdapter;
        _captureStore = captureStore;
        _timeProvider = timeProvider;
    }

    public McpCapture Capture(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Capture region width and height must be greater than zero.");
        }

        var region = new Rectangle(x, y, width, height);
        MonitorCaptureResult[] monitors = _screenCapture.CaptureAllMonitors();
        if (monitors.Length == 0)
        {
            throw new InvalidOperationException("No monitors are available to capture.");
        }

        if (!monitors.Any(monitor => monitor.MonitorBounds.IntersectsWith(region)))
        {
            throw new InvalidOperationException("The requested capture region does not intersect any monitor.");
        }

        MonitorCaptureResult referenceMonitor = monitors
            .OrderByDescending(monitor => Rectangle.Intersect(monitor.MonitorBounds, region).Width * Rectangle.Intersect(monitor.MonitorBounds, region).Height)
            .First();

        MonitorCaptureResult? containingMonitor = monitors
            .Cast<MonitorCaptureResult?>()
            .FirstOrDefault(monitor => monitor!.Value.MonitorBounds.Contains(region));

        if (containingMonitor is not null)
        {
            MonitorCaptureResult monitor = containingMonitor.Value;
            return _imageCaptureAdapter.Capture(
                CaptureTarget.Rectangle(
                    monitor.HMonitor,
                    region.X - monitor.MonitorBounds.X,
                    region.Y - monitor.MonitorBounds.Y,
                    region.Width,
                    region.Height),
                region,
                "region",
                monitor.Dpi,
                monitor.Scale,
                monitorBounds: monitor.MonitorBounds,
                workAreaBounds: monitor.WorkAreaBounds,
                isPrimary: monitor.IsPrimary);
        }

        using Bitmap regionBitmap = CaptureSpanningRegionBitmap(region, monitors);
        using var stream = new MemoryStream();
        regionBitmap.Save(stream, ImageFormat.Png);

        var metadata = McpCaptureMetadata.Create(
            McpCaptureIds.Create(),
            _timeProvider.GetUtcNow(),
            regionBitmap.Width,
            regionBitmap.Height,
            referenceMonitor.Dpi,
            referenceMonitor.Scale,
            region,
            "region",
            "png");

        var capture = new McpCapture(stream.ToArray(), metadata);
        _captureStore.Store(capture);

        return capture;
    }

    private Bitmap CaptureSpanningRegionBitmap(Rectangle region, MonitorCaptureResult[] monitors)
    {
        Rectangle combinedBounds = CaptureTargetSelection.CombineBounds(monitors.Select(monitor => monitor.MonitorBounds));
        if (!combinedBounds.Contains(region))
        {
            throw new InvalidOperationException("The requested capture region must be fully inside the combined monitor bounds.");
        }

        using Bitmap combinedBitmap = _screenCapture.CombineMonitors(monitors);
        var combinedSourceRegion = new Rectangle(
            region.X - combinedBounds.X,
            region.Y - combinedBounds.Y,
            region.Width,
            region.Height);

        return CropBitmap(combinedBitmap, combinedSourceRegion, region.Size);
    }

    private static Bitmap CropBitmap(Bitmap source, Rectangle sourceRegion, Size outputSize)
    {
        var output = new Bitmap(outputSize.Width, outputSize.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(output);
        graphics.DrawImage(source, new Rectangle(Point.Empty, outputSize), sourceRegion, GraphicsUnit.Pixel);
        return output;
    }

}
