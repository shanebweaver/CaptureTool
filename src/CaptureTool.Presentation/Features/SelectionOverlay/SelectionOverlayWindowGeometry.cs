using CaptureTool.Domain.Capture;
using System.Drawing;

namespace CaptureTool.Presentation.Features.SelectionOverlay;

public static class SelectionOverlayWindowGeometry
{
    public static bool TryProjectToMonitor(
        WindowInfo window,
        MonitorCaptureResult monitor,
        out WindowInfo projectedWindow)
    {
        if (monitor.Scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monitor), "Monitor scale must be greater than zero.");
        }

        Rectangle intersection = Rectangle.Intersect(window.Position, monitor.MonitorBounds);
        if (intersection.Width <= 0 || intersection.Height <= 0)
        {
            projectedWindow = default;
            return false;
        }

        int canvasWidth = Math.Max(1, (int)Math.Floor(monitor.MonitorBounds.Width / monitor.Scale));
        int canvasHeight = Math.Max(1, (int)Math.Floor(monitor.MonitorBounds.Height / monitor.Scale));

        int left = ProjectLeadingEdge(intersection.Left - monitor.MonitorBounds.Left, monitor.Scale, canvasWidth);
        int top = ProjectLeadingEdge(intersection.Top - monitor.MonitorBounds.Top, monitor.Scale, canvasHeight);
        int right = ProjectTrailingEdge(intersection.Right - monitor.MonitorBounds.Left, monitor.Scale, canvasWidth);
        int bottom = ProjectTrailingEdge(intersection.Bottom - monitor.MonitorBounds.Top, monitor.Scale, canvasHeight);

        PreservePhysicalIntersection(ref left, ref right, canvasWidth);
        PreservePhysicalIntersection(ref top, ref bottom, canvasHeight);

        if (right <= left || bottom <= top)
        {
            projectedWindow = default;
            return false;
        }

        projectedWindow = new WindowInfo(
            window.Handle,
            window.Title,
            Rectangle.FromLTRB(left, top, right, bottom));
        return true;
    }

    private static int ProjectLeadingEdge(int physicalCoordinate, float scale, int logicalLimit)
        => Math.Clamp((int)Math.Floor(physicalCoordinate / scale), 0, logicalLimit);

    private static int ProjectTrailingEdge(int physicalCoordinate, float scale, int logicalLimit)
        => Math.Clamp((int)Math.Ceiling(physicalCoordinate / scale), 0, logicalLimit);

    private static void PreservePhysicalIntersection(ref int leadingEdge, ref int trailingEdge, int logicalLimit)
    {
        if (trailingEdge > leadingEdge)
        {
            return;
        }

        if (trailingEdge == logicalLimit)
        {
            leadingEdge = Math.Max(0, logicalLimit - 1);
        }
        else
        {
            trailingEdge = Math.Min(logicalLimit, leadingEdge + 1);
        }
    }
}
