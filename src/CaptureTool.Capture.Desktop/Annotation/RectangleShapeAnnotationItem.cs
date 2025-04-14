using System.Drawing;

namespace CaptureTool.Capture.Desktop.Annotation;

public sealed partial class RectangleShapeAnnotationItem : ShapeAnnotationItem
{
    public int Height { get; set; }
    public int Width { get; set; }
    public Color Color { get; set; }
    public int StrokeWidth { get; set; }

    public RectangleShapeAnnotationItem(int left, int top, int height, int width, Color color, int strokeWidth) : base(left, top)
    {
        Height = height;
        Width = width;
        Color = color;
        StrokeWidth = strokeWidth;
    }
}
