using CaptureTool.Domain.Capture;
using CaptureTool.Mcp.CaptureServer.Models;
using System.Drawing;

namespace CaptureTool.Mcp.CaptureServer.Capture;

internal sealed record CaptureDisplayMetadata(
    uint? Dpi,
    float? Scale,
    MonitorSegmentDto[] MonitorSegments)
{
    public static CaptureDisplayMetadata Create(
        IReadOnlyList<MonitorCaptureResult> monitors,
        Rectangle captureSourceBounds)
    {
        MonitorSegmentDto[] segments =
        [
            .. monitors
                .Select(monitor => (Monitor: monitor, Intersection: Rectangle.Intersect(monitor.MonitorBounds, captureSourceBounds)))
                .Where(item => item.Intersection.Width > 0 && item.Intersection.Height > 0)
                .Select(item => new MonitorSegmentDto(
                    $"hmonitor:{item.Monitor.HMonitor}",
                    RectangleDto.FromRectangle(item.Intersection),
                    RectangleDto.FromRectangle(new Rectangle(
                        item.Intersection.X - captureSourceBounds.X,
                        item.Intersection.Y - captureSourceBounds.Y,
                        item.Intersection.Width,
                        item.Intersection.Height)),
                    item.Monitor.Dpi,
                    item.Monitor.Scale,
                    item.Monitor.IsPrimary)),
        ];

        if (segments.Length == 0)
        {
            throw new InvalidOperationException("Capture bounds do not intersect an available monitor.");
        }

        uint firstDpi = segments[0].Dpi;
        bool isUniform = segments.All(segment => segment.Dpi == firstDpi);
        return new CaptureDisplayMetadata(
            isUniform ? firstDpi : null,
            isUniform ? firstDpi / 96f : null,
            segments);
    }
}
