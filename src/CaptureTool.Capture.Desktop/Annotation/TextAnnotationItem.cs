using System.Drawing;

namespace CaptureTool.Capture.Desktop.Annotation;

public sealed partial class TextAnnotationItem : AnnotationItem
{
    public string Text { get; set; }
    public Color Color { get; set; }

    public TextAnnotationItem(string text, int left, int top, Color color) : base(left, top)
    {
        Text = text;
        Color = color;
    }
}
