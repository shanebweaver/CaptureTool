using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit.Drawable;

public static class DrawableFactory
{
    private const float MinimumDrawableSize = 2f;

    public static IDrawable? CreateShape(ShapeType shapeType, Vector2 startPoint, Vector2 endPoint, ShapeStyle style)
    {
        return shapeType switch
        {
            ShapeType.Rectangle => CreateRectangle(startPoint, endPoint, style),
            ShapeType.Ellipse => CreateEllipse(startPoint, endPoint, style),
            ShapeType.Line => CreateLine(startPoint, endPoint, style),
            ShapeType.Arrow => CreateArrow(startPoint, endPoint, style),
            _ => null,
        };
    }

    public static TextDrawable? CreateTextBox(Vector2 startPoint, Vector2 endPoint, TextStyle style)
    {
        RectangleF bounds = GetBounds(startPoint, endPoint);
        if (bounds.Width < MinimumDrawableSize || bounds.Height < MinimumDrawableSize)
        {
            return null;
        }

        return new TextDrawable(
            new Vector2(bounds.X, bounds.Y),
            ToCeilingSize(bounds),
            string.Empty,
            style.FontColor,
            style.BackgroundColor,
            style.FontFamily,
            style.FontSize);
    }

    private static RectangleDrawable? CreateRectangle(Vector2 startPoint, Vector2 endPoint, ShapeStyle style)
    {
        RectangleF bounds = GetBounds(startPoint, endPoint);
        if (bounds.Width < MinimumDrawableSize || bounds.Height < MinimumDrawableSize)
        {
            return null;
        }

        return new RectangleDrawable(
            new Vector2(bounds.X, bounds.Y),
            ToCeilingSize(bounds),
            style.StrokeColor,
            style.FillColor,
            style.StrokeWidth);
    }

    private static EllipseDrawable? CreateEllipse(Vector2 startPoint, Vector2 endPoint, ShapeStyle style)
    {
        RectangleF bounds = GetBounds(startPoint, endPoint);
        if (bounds.Width < MinimumDrawableSize || bounds.Height < MinimumDrawableSize)
        {
            return null;
        }

        return new EllipseDrawable(
            new Vector2(bounds.X, bounds.Y),
            ToCeilingSize(bounds),
            style.StrokeColor,
            style.FillColor,
            style.StrokeWidth);
    }

    private static LineDrawable? CreateLine(Vector2 startPoint, Vector2 endPoint, ShapeStyle style)
    {
        if (Vector2.Distance(startPoint, endPoint) < MinimumDrawableSize)
        {
            return null;
        }

        return new LineDrawable(startPoint, endPoint, style.StrokeColor, style.StrokeWidth);
    }

    private static ArrowDrawable? CreateArrow(Vector2 startPoint, Vector2 endPoint, ShapeStyle style)
    {
        if (Vector2.Distance(startPoint, endPoint) < MinimumDrawableSize)
        {
            return null;
        }

        return new ArrowDrawable(startPoint, endPoint, style.StrokeColor, style.StrokeWidth);
    }

    private static RectangleF GetBounds(Vector2 startPoint, Vector2 endPoint)
    {
        float x = Math.Min(startPoint.X, endPoint.X);
        float y = Math.Min(startPoint.Y, endPoint.Y);
        float width = Math.Abs(endPoint.X - startPoint.X);
        float height = Math.Abs(endPoint.Y - startPoint.Y);

        return new RectangleF(x, y, width, height);
    }

    private static Size ToCeilingSize(RectangleF bounds)
    {
        return new Size((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));
    }
}
