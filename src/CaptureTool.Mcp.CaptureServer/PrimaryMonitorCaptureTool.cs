using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace CaptureTool.Mcp.CaptureServer;

[McpServerToolType]
public sealed class PrimaryMonitorCaptureTool
{
    [McpServerTool(
        Name = "capture_primary_monitor",
        Title = "Capture Primary Monitor",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    [Description("Captures the current image of the primary monitor on the local Windows desktop and returns it as a PNG image for user-visible progress verification.")]
    public static CallToolResult CapturePrimaryMonitor(
        IPrimaryMonitorCaptureService captureService,
        ILogger<PrimaryMonitorCaptureTool> logger,
        [Description("Short explanation of why the agent is requesting this capture.")]
        string? reason = null)
    {
        try
        {
            PrimaryMonitorCapture capture = captureService.Capture();
            PrimaryMonitorCaptureMetadata metadata = capture.Metadata;
            string trimmedReason = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim();

            logger.LogInformation(
                "Captured primary monitor at {CapturedAtUtc}: {Width}x{Height} {Format}. Reason: {Reason}",
                metadata.CapturedAtUtc,
                metadata.Width,
                metadata.Height,
                metadata.Format,
                trimmedReason);

            return new CallToolResult
            {
                IsError = false,
                Content =
                [
                    new TextContentBlock
                    {
                        Text = $"Captured primary monitor at {metadata.CapturedAtUtc:O}, {metadata.Width}x{metadata.Height} PNG.",
                    },
                    ImageContentBlock.FromBytes(capture.PngBytes, "image/png"),
                ],
                StructuredContent = JsonSerializer.SerializeToElement(
                    metadata,
                    PrimaryMonitorCaptureJsonSerializerContext.Default.PrimaryMonitorCaptureMetadata),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Primary monitor capture failed. Reason: {Reason}", reason);

            return new CallToolResult
            {
                IsError = true,
                Content =
                [
                    new TextContentBlock
                    {
                        Text = $"Primary monitor capture failed: {ex.Message}",
                    },
                ],
            };
        }
    }
}
