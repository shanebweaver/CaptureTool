using System.Drawing;
using System.Text.Json.Serialization;

namespace CaptureTool.Mcp.CaptureServer;

public sealed record PrimaryMonitorCaptureMetadata(
    DateTimeOffset CapturedAtUtc,
    int Width,
    int Height,
    uint Dpi,
    float Scale,
    RectangleDto MonitorBounds,
    RectangleDto WorkAreaBounds,
    bool IsPrimary,
    string Format)
{
    public static PrimaryMonitorCaptureMetadata Create(
        DateTimeOffset capturedAtUtc,
        int width,
        int height,
        uint dpi,
        float scale,
        Rectangle monitorBounds,
        Rectangle workAreaBounds,
        bool isPrimary,
        string format)
        => new(
            capturedAtUtc,
            width,
            height,
            dpi,
            scale,
            RectangleDto.FromRectangle(monitorBounds),
            RectangleDto.FromRectangle(workAreaBounds),
            isPrimary,
            format);
}

public sealed record RectangleDto(int X, int Y, int Width, int Height)
{
    public static RectangleDto FromRectangle(Rectangle rectangle)
        => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PrimaryMonitorCaptureMetadata))]
internal sealed partial class PrimaryMonitorCaptureJsonSerializerContext : JsonSerializerContext;
