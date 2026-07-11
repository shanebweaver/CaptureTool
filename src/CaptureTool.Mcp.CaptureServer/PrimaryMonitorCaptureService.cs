using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;
using System.Drawing.Imaging;

namespace CaptureTool.Mcp.CaptureServer;

public sealed class PrimaryMonitorCaptureService : IPrimaryMonitorCaptureService
{
    private readonly IScreenCapture _screenCapture;
    private readonly TimeProvider _timeProvider;

    public PrimaryMonitorCaptureService(IScreenCapture screenCapture, TimeProvider timeProvider)
    {
        _screenCapture = screenCapture;
        _timeProvider = timeProvider;
    }

    public PrimaryMonitorCapture Capture()
    {
        MonitorCaptureResult[] monitors = _screenCapture.CaptureAllMonitors();
        MonitorCaptureResult primaryMonitor = monitors.FirstOrDefault(monitor => monitor.IsPrimary);

        if (!primaryMonitor.IsPrimary)
        {
            throw new InvalidOperationException("No primary monitor is available to capture.");
        }

        using var bitmap = _screenCapture.CreateBitmapFromMonitorCaptureResult(primaryMonitor);
        using var stream = new MemoryStream();

        bitmap.Save(stream, ImageFormat.Png);

        var metadata = PrimaryMonitorCaptureMetadata.Create(
            _timeProvider.GetUtcNow(),
            bitmap.Width,
            bitmap.Height,
            primaryMonitor.Dpi,
            primaryMonitor.Scale,
            primaryMonitor.MonitorBounds,
            primaryMonitor.WorkAreaBounds,
            primaryMonitor.IsPrimary,
            "png");

        return new PrimaryMonitorCapture(stream.ToArray(), metadata);
    }
}
