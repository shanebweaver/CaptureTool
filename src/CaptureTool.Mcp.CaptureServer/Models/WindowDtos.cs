namespace CaptureTool.Mcp.CaptureServer.Models;

public sealed record WindowInfoDto(string WindowId, string Title, RectangleDto Bounds);

public sealed record ListWindowsResult(WindowInfoDto[] Windows);
