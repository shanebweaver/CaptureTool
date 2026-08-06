using System.Drawing;

namespace CaptureTool.Mcp.CaptureServer.Models;

public sealed record McpCaptureMetadata(
    string CaptureId,
    DateTimeOffset CapturedAtUtc,
    int Width,
    int Height,
    uint? Dpi,
    float? Scale,
    RectangleDto SourceBounds,
    RectangleDto ImageBounds,
    string SourceKind,
    string Format,
    RectangleDto? MonitorBounds = null,
    RectangleDto? WorkAreaBounds = null,
    bool? IsPrimary = null,
    string? TargetId = null,
    string? TargetTitle = null,
    string? SourceCaptureId = null,
    AnnotationPlacementDto[]? AnnotationPlacements = null,
    MonitorSegmentDto[]? MonitorSegments = null)
{
    public bool IsDpiScaleUniform => Dpi.HasValue && Scale.HasValue;

    public static McpCaptureMetadata Create(
        string captureId,
        DateTimeOffset capturedAtUtc,
        int width,
        int height,
        uint? dpi,
        float? scale,
        Rectangle sourceBounds,
        string sourceKind,
        string format,
        Rectangle? monitorBounds = null,
        Rectangle? workAreaBounds = null,
        bool? isPrimary = null,
        string? targetId = null,
        string? targetTitle = null,
        string? sourceCaptureId = null,
        AnnotationPlacementDto[]? annotationPlacements = null,
        MonitorSegmentDto[]? monitorSegments = null)
        => new(
            captureId,
            capturedAtUtc,
            width,
            height,
            dpi,
            scale,
            RectangleDto.FromRectangle(sourceBounds),
            RectangleDto.FromRectangle(new Rectangle(0, 0, width, height)),
            sourceKind,
            format,
            monitorBounds is null ? null : RectangleDto.FromRectangle(monitorBounds.Value),
            workAreaBounds is null ? null : RectangleDto.FromRectangle(workAreaBounds.Value),
            isPrimary,
            targetId,
            targetTitle,
            sourceCaptureId,
            annotationPlacements,
            monitorSegments);
}
