namespace CaptureTool.Capture.Desktop.Annotation;

public sealed partial class TextAnnotationItem : AnnotationItem
{
    public string Text { get; set; }

    public TextAnnotationItem(string text, int left, int top) : base(left, top)
    {
        Text = text;
    }
}
