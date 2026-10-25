using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Mcp.CaptureServer.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CaptureTool.Mcp.CaptureServer.Tools;

[McpServerToolType]
public sealed class AnnotationTool
{
    [McpServerTool(
        Name = "annotate_image",
        Title = "Annotate Image",
        ReadOnly = false,
        Destructive = false,
        OpenWorld = false)]
    [Description("Adds an arrow and optional nearby text label to a previous CaptureTool MCP image capture.")]
    public static CallToolResult AnnotateImage(
        IAnnotationService annotationService,
        ILogger<AnnotationTool> logger,
        [Description("Capture ID returned by a previous image capture tool. Captures are retained up to 20 images or 128 MiB of PNG data, with a 32 MiB limit per image; older captures may be evicted. Recapture if unavailable.")]
        string captureId,
        [Description("Arrow start X coordinate in image pixels, clamped to the image bounds.")]
        int arrowStartX,
        [Description("Arrow start Y coordinate in image pixels, clamped to the image bounds.")]
        int arrowStartY,
        [Description("Arrow end X coordinate in image pixels, clamped to the image bounds.")]
        int arrowEndX,
        [Description("Arrow end Y coordinate in image pixels, clamped to the image bounds.")]
        int arrowEndY,
        [Description("Optional text label, at most 1024 UTF-16 characters, rendered near the arrow within the image bounds.")]
        string? label = null)
    {
        try
        {
            McpCapture capture = annotationService.AnnotateWithArrow(
                captureId,
                arrowStartX,
                arrowStartY,
                arrowEndX,
                arrowEndY,
                label);

            logger.LogInformation(
                "Annotated capture {SourceCaptureId} into {CaptureId}",
                capture.Metadata.SourceCaptureId,
                capture.Metadata.CaptureId);

            return McpToolResultFactory.Image(
                capture,
                $"Annotated capture {capture.Metadata.SourceCaptureId} at {capture.Metadata.CapturedAtUtc:O}, {capture.Metadata.Width}x{capture.Metadata.Height} PNG.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image annotation failed for capture {CaptureId}", captureId);
            return McpToolResultFactory.TextError($"Image annotation failed: {ex.Message}");
        }
    }
}
