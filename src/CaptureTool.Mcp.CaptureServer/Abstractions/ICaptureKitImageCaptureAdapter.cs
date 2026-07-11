using CaptureTool.Mcp.CaptureServer.Models;
using CaptureKit.Abstractions;
using System.Drawing;

namespace CaptureTool.Mcp.CaptureServer.Abstractions;

public interface ICaptureKitImageCaptureAdapter
{
    McpCapture Capture(
        CaptureTarget target,
        Rectangle sourceBounds,
        string sourceKind,
        uint dpi,
        float scale,
        string? targetId = null,
        string? targetTitle = null,
        Rectangle? monitorBounds = null,
        Rectangle? workAreaBounds = null,
        bool? isPrimary = null);
}
