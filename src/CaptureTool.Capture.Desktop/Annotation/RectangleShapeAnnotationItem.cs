namespace CaptureTool.Capture.Desktop.Annotation;

public sealed partial class RectangleShapeAnnotationItem : ShapeAnnotationItem
{
    public int Height { get; set; }
    public int Width { get; set; }

    public RectangleShapeAnnotationItem(int left, int top, int height, int width) : base(left, top)
    {
        Height = height;
        Width = width;
    }
}
