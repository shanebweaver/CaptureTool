using System.Drawing;

namespace CaptureTool.Application.Abstractions.Capture;

/// <summary>Enumerates desktop monitor geometry without reading screen pixels.</summary>
public interface IDisplayTopology
{
    IReadOnlyList<Rectangle> GetMonitorBounds();
}
