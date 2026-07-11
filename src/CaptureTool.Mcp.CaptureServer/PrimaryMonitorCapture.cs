namespace CaptureTool.Mcp.CaptureServer;

public sealed record PrimaryMonitorCapture(byte[] PngBytes, PrimaryMonitorCaptureMetadata Metadata);
