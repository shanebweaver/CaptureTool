namespace CaptureTool.Mcp.CaptureServer.Models;

public sealed record AnnotationPlacementDto(string Kind, RectangleDto Bounds, string? Label);
