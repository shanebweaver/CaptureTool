using CaptureTool.Mcp.CaptureServer.Models;
namespace CaptureTool.Mcp.CaptureServer.Abstractions;

public interface IPrimaryMonitorCaptureService
{
    McpCapture Capture();
}
