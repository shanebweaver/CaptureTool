using CaptureTool.Mcp.CaptureServer.Models;
namespace CaptureTool.Mcp.CaptureServer.Abstractions;

public interface IWindowCaptureService
{
    IReadOnlyList<WindowInfoDto> ListWindows();

    McpCapture CaptureWindow(string windowId);
}
