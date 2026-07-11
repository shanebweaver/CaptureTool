using CaptureTool.Mcp.CaptureServer.Models;
namespace CaptureTool.Mcp.CaptureServer.Abstractions;

public interface IAllScreensCaptureService
{
    McpCapture Capture();
}
