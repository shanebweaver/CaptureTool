using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace CaptureTool.Mcp.CaptureServer.Tools;

internal static class McpToolResultFactory
{
    public static CallToolResult Image(McpCapture capture, string text)
        => new()
        {
            IsError = false,
            Content =
            [
                new TextContentBlock
                {
                    Text = text,
                },
                ImageContentBlock.FromBytes(capture.PngBytes, "image/png"),
            ],
            StructuredContent = JsonSerializer.SerializeToElement(
                capture.Metadata,
                McpCaptureJsonSerializerContext.Default.McpCaptureMetadata),
        };

    public static CallToolResult TextError(string message)
        => new()
        {
            IsError = true,
            Content =
            [
                new TextContentBlock
                {
                    Text = message,
                },
            ],
        };
}
