namespace CaptureTool.Mcp.CaptureServer.Capture;

internal static class McpCaptureIds
{
    public static string Create() => $"capture:{Guid.NewGuid():N}";
}
