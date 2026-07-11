using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CaptureTool.Mcp.CaptureServer.Tools;

[McpServerToolType]
public sealed class RegionCaptureTool
{
    [McpServerTool(
        Name = "capture_region",
        Title = "Capture Region",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    [Description("Captures a rectangular region of the local Windows desktop and returns it as a PNG image.")]
    public static CallToolResult CaptureRegion(
        IRegionCaptureService captureService,
        ILogger<RegionCaptureTool> logger,
        [Description("The left edge of the region in virtual-screen coordinates.")]
        int x,
        [Description("The top edge of the region in virtual-screen coordinates.")]
        int y,
        [Description("The region width in pixels.")]
        int width,
        [Description("The region height in pixels.")]
        int height,
        [Description("Short explanation of why the agent is requesting this capture.")]
        string? reason = null)
    {
        try
        {
            McpCapture capture = captureService.Capture(x, y, width, height);
            McpCaptureMetadata metadata = capture.Metadata;
            string trimmedReason = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim();

            logger.LogInformation(
                "Captured region {X},{Y} {Width}x{Height} at {CapturedAtUtc}. Reason: {Reason}",
                x,
                y,
                width,
                height,
                metadata.CapturedAtUtc,
                trimmedReason);

            return McpToolResultFactory.Image(
                capture,
                $"Captured region at {metadata.CapturedAtUtc:O}, {metadata.Width}x{metadata.Height} PNG.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Region capture failed. Reason: {Reason}", reason);
            return McpToolResultFactory.TextError($"Region capture failed: {ex.Message}");
        }
    }
}
