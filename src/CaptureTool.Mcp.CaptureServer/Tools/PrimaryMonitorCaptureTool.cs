using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Mcp.CaptureServer.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CaptureTool.Mcp.CaptureServer.Tools;

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
            McpCapture capture = captureService.Capture();
            McpCaptureMetadata metadata = capture.Metadata;
            string trimmedReason = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim();

            logger.LogInformation(
                "Captured primary monitor at {CapturedAtUtc}: {Width}x{Height} {Format}. Reason: {Reason}",
                metadata.CapturedAtUtc,
                metadata.Width,
                metadata.Height,
                metadata.Format,
                trimmedReason);

            return McpToolResultFactory.Image(
                capture,
                $"Captured primary monitor at {metadata.CapturedAtUtc:O}, {metadata.Width}x{metadata.Height} PNG.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Primary monitor capture failed. Reason: {Reason}", reason);

            return McpToolResultFactory.TextError($"Primary monitor capture failed: {ex.Message}");
        }
    }
}
