namespace CaptureTool.Capture.Desktop.Annotation;

public abstract partial class AnnotationItem
{
    public int Left { get; set; }
    public int Top { get; set; }

    public AnnotationItem(int left, int top)
    {
        Left = left;
        Top = top;
    }
}

