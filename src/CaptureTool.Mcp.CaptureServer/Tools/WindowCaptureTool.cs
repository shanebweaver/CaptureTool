using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace CaptureTool.Mcp.CaptureServer.Tools;

[McpServerToolType]
public sealed class WindowCaptureTool
{
    [McpServerTool(
        Name = "list_windows",
        Title = "List Windows",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    [Description("Lists local Windows desktop windows available for MCP image capture.")]
    public static CallToolResult ListWindows(
        IWindowCaptureService windowCaptureService,
        ILogger<WindowCaptureTool> logger)
    {
        try
        {
            IReadOnlyList<WindowInfoDto> windows = windowCaptureService.ListWindows();
            logger.LogInformation("Listed {WindowCount} windows for MCP capture.", windows.Count);

            return new CallToolResult
            {
                IsError = false,
                Content =
                [
                    new TextContentBlock
                    {
                        Text = $"Found {windows.Count} capturable windows.",
                    },
                ],
                StructuredContent = JsonSerializer.SerializeToElement(
                    new ListWindowsResult([.. windows]),
                    McpCaptureJsonSerializerContext.Default.ListWindowsResult),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Window listing failed.");
            return McpToolResultFactory.TextError($"Window listing failed: {ex.Message}");
        }
    }

    [McpServerTool(
        Name = "capture_window",
        Title = "Capture Window",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    [Description("Captures a local Windows desktop window by ID using CaptureKit window image capture and returns it as a PNG image.")]
    public static CallToolResult CaptureWindow(
        IWindowCaptureService windowCaptureService,
        ILogger<WindowCaptureTool> logger,
        [Description("Window ID returned by list_windows, using the hwnd:<handle> format.")]
        string windowId,
        [Description("Short explanation of why the agent is requesting this capture.")]
        string? reason = null)
    {
        try
        {
            McpCapture capture = windowCaptureService.CaptureWindow(windowId);
            McpCaptureMetadata metadata = capture.Metadata;
            string trimmedReason = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim();

            logger.LogInformation(
                "Captured window {WindowId} at {CapturedAtUtc}: {Width}x{Height}. Reason: {Reason}",
                windowId,
                metadata.CapturedAtUtc,
                metadata.Width,
                metadata.Height,
                trimmedReason);

            return McpToolResultFactory.Image(
                capture,
                $"Captured window {metadata.TargetTitle ?? windowId} at {metadata.CapturedAtUtc:O}, {metadata.Width}x{metadata.Height} PNG.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Window capture failed for {WindowId}. Reason: {Reason}", windowId, reason);
            return McpToolResultFactory.TextError($"Window capture failed: {ex.Message}");
        }
    }
}
