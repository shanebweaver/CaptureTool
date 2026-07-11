using System.Drawing;

namespace CaptureTool.Mcp.CaptureServer.Models;

public sealed record RectangleDto(int X, int Y, int Width, int Height)
{
    public static RectangleDto FromRectangle(Rectangle rectangle)
        => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    public Rectangle ToRectangle() => new(X, Y, Width, Height);
}

public sealed record PointDto(int X, int Y);
