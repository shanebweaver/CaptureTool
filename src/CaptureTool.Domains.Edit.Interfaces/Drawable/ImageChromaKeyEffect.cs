using System.Drawing;

namespace CaptureTool.Domains.Edit.Interfaces.Drawable;

public partial class ImageChromaKeyEffect : IImageEffect
{
    public Color Color { get; set; }
    public float Tolerance { get; set; }
    public float Desaturation { get; set; }
    public bool IsEnabled { get; set; }

    public ImageChromaKeyEffect(Color color, float tolerance, float desaturation)
    {
        Color = color;
        Tolerance = tolerance;
        Desaturation = desaturation;
    }
}
