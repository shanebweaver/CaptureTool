using CaptureTool.Domain.Capture;
using System.Drawing;

namespace CaptureTool.Mcp.CaptureServer.Capture;

internal static class CaptureTargetSelection
{
    public static MonitorCaptureResult GetPrimaryMonitor(IReadOnlyList<MonitorCaptureResult> monitors)
    {
        MonitorCaptureResult primaryMonitor = monitors.FirstOrDefault(monitor => monitor.IsPrimary);
        if (!primaryMonitor.IsPrimary)
        {
            throw new InvalidOperationException("No primary monitor is available to capture.");
        }

        return primaryMonitor;
    }

    public static MonitorCaptureResult GetBestMonitorForBounds(IReadOnlyList<MonitorCaptureResult> monitors, Rectangle bounds)
    {
        if (monitors.Count == 0)
        {
            throw new InvalidOperationException("No monitors are available to capture.");
        }

        return monitors
            .OrderByDescending(monitor => GetIntersectionArea(monitor.MonitorBounds, bounds))
            .ThenByDescending(monitor => monitor.IsPrimary)
            .First();
    }

    public static Rectangle CombineBounds(IEnumerable<Rectangle> rectangles)
    {
        using IEnumerator<Rectangle> enumerator = rectangles.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return Rectangle.Empty;
        }

        Rectangle combined = enumerator.Current;
        while (enumerator.MoveNext())
        {
            combined = Rectangle.Union(combined, enumerator.Current);
        }

        return combined;
    }

    private static int GetIntersectionArea(Rectangle first, Rectangle second)
    {
        Rectangle intersection = Rectangle.Intersect(first, second);
        return intersection.Width * intersection.Height;
    }
}
