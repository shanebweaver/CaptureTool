using System.Drawing;

namespace CaptureTool.Edit.Drawable;

public partial class ImageChromaKeyEffect : IImageEffect
{
    public Color Color { get; set; }
    public float Tolerance { get; set; }
    public bool IsEnabled { get; set; }

    public ImageChromaKeyEffect(Color color, float tolerance)
    {
        Color = color;
        Tolerance = tolerance;
    }
}
