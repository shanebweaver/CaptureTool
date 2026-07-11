using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;
using System.Drawing.Imaging;

namespace CaptureTool.Mcp.CaptureServer.Capture;

public sealed class PrimaryMonitorCaptureService : IPrimaryMonitorCaptureService
{
    private readonly IScreenCapture _screenCapture;
    private readonly IMcpCaptureStore _captureStore;
    private readonly TimeProvider _timeProvider;

    public PrimaryMonitorCaptureService(
        IScreenCapture screenCapture,
        IMcpCaptureStore captureStore,
        TimeProvider timeProvider)
    {
        _screenCapture = screenCapture;
        _captureStore = captureStore;
        _timeProvider = timeProvider;
    }

    public McpCapture Capture()
    {
        MonitorCaptureResult[] monitors = _screenCapture.CaptureAllMonitors();
        MonitorCaptureResult primaryMonitor = CaptureTargetSelection.GetPrimaryMonitor(monitors);
        using var bitmap = _screenCapture.CreateBitmapFromMonitorCaptureResult(primaryMonitor);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        var metadata = McpCaptureMetadata.Create(
            McpCaptureIds.Create(),
            _timeProvider.GetUtcNow(),
            bitmap.Width,
            bitmap.Height,
            primaryMonitor.Dpi,
            primaryMonitor.Scale,
            primaryMonitor.MonitorBounds,
            "primaryMonitor",
            "png",
            targetId: $"hmonitor:{primaryMonitor.HMonitor}",
            targetTitle: "Primary monitor",
            monitorBounds: primaryMonitor.MonitorBounds,
            workAreaBounds: primaryMonitor.WorkAreaBounds,
            isPrimary: primaryMonitor.IsPrimary);

        var capture = new McpCapture(stream.ToArray(), metadata);
        _captureStore.Store(capture);
        return capture;
    }
}
