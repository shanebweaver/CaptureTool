using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
namespace CaptureTool.Mcp.CaptureServer.Capture;

internal static class McpCaptureIds
{
    public static string Create() => $"capture:{Guid.NewGuid():N}";
}
