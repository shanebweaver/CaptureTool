using CaptureTool.Mcp.CaptureServer.Models;
namespace CaptureTool.Mcp.CaptureServer.Abstractions;

public interface IMcpCaptureStore
{
    void Store(McpCapture capture);

    bool TryGet(string captureId, out McpCapture capture);
}
