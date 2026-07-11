using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;
using System.Drawing;
using System.Drawing.Imaging;

namespace CaptureTool.Mcp.CaptureServer.Capture;

public sealed class AllScreensCaptureService : IAllScreensCaptureService
{
    private readonly IScreenCapture _screenCapture;
    private readonly IMcpCaptureStore _captureStore;
    private readonly TimeProvider _timeProvider;

    public AllScreensCaptureService(IScreenCapture screenCapture, IMcpCaptureStore captureStore, TimeProvider timeProvider)
    {
        _screenCapture = screenCapture;
        _captureStore = captureStore;
        _timeProvider = timeProvider;
    }

    public McpCapture Capture()
    {
        MonitorCaptureResult[] monitors = _screenCapture.CaptureAllMonitors();
        if (monitors.Length == 0)
        {
            throw new InvalidOperationException("No monitors are available to capture.");
        }

        MonitorCaptureResult referenceMonitor = monitors.FirstOrDefault(monitor => monitor.IsPrimary);
        if (!referenceMonitor.IsPrimary)
        {
            referenceMonitor = monitors[0];
        }

        using var bitmap = _screenCapture.CombineMonitors(monitors);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        Rectangle sourceBounds = CaptureTargetSelection.CombineBounds(monitors.Select(monitor => monitor.MonitorBounds));
        var metadata = McpCaptureMetadata.Create(
            McpCaptureIds.Create(),
            _timeProvider.GetUtcNow(),
            bitmap.Width,
            bitmap.Height,
            referenceMonitor.Dpi,
            referenceMonitor.Scale,
            sourceBounds,
            "allScreens",
            "png");

        var capture = new McpCapture(stream.ToArray(), metadata);
        _captureStore.Store(capture);
        return capture;
    }
}
