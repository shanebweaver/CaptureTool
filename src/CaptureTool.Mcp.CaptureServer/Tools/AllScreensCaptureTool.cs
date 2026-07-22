using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Mcp.CaptureServer.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CaptureTool.Mcp.CaptureServer.Tools;

[McpServerToolType]
public sealed class AllScreensCaptureTool
{
    [McpServerTool(
        Name = "capture_all_screens",
        Title = "Capture All Screens",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    [Description("Captures all local Windows monitors into one combined PNG image.")]
    public static CallToolResult CaptureAllScreens(
        IAllScreensCaptureService captureService,
        ILogger<AllScreensCaptureTool> logger,
        [Description("Short explanation of why the agent is requesting this capture.")]
        string? reason = null)
    {
        try
        {
            McpCapture capture = captureService.Capture();
            McpCaptureMetadata metadata = capture.Metadata;
            string trimmedReason = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim();

            logger.LogInformation(
                "Captured all screens at {CapturedAtUtc}: {Width}x{Height}. Reason: {Reason}",
                metadata.CapturedAtUtc,
                metadata.Width,
                metadata.Height,
                trimmedReason);

            return McpToolResultFactory.Image(
                capture,
                $"Captured all screens at {metadata.CapturedAtUtc:O}, {metadata.Width}x{metadata.Height} PNG.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "All-screens capture failed. Reason: {Reason}", reason);
            return McpToolResultFactory.TextError($"All-screens capture failed: {ex.Message}");
        }
    }
}
