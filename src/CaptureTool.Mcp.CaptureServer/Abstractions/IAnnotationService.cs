using CaptureTool.Mcp.CaptureServer.Models;
namespace CaptureTool.Mcp.CaptureServer.Abstractions;

public interface IAnnotationService
{
    McpCapture AnnotateWithArrow(string captureId, int arrowStartX, int arrowStartY, int arrowEndX, int arrowEndY, string? label);
}
