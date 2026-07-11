using CaptureTool.Mcp.CaptureServer.Models;
namespace CaptureTool.Mcp.CaptureServer.Abstractions;

public interface IRegionCaptureService
{
    McpCapture Capture(int x, int y, int width, int height);
}
