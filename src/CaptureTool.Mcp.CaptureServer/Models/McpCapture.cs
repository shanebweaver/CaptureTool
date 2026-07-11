namespace CaptureTool.Mcp.CaptureServer.Models;

public sealed record McpCapture(byte[] PngBytes, McpCaptureMetadata Metadata);
