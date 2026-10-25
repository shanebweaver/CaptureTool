using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record RecognizedQrCodeRegion(
    string Value,
    RectangleF Bounds)
{
    public bool OverlapsText(RectangleF textBounds)
    {
        if (textBounds.Width <= 0 || textBounds.Height <= 0) return false;
        RectangleF exclusion = Bounds;
        float padding = Math.Clamp(Math.Min(Bounds.Width, Bounds.Height) * .04f, 2, 10);
        exclusion.Inflate(padding, padding);
        var center = new PointF(textBounds.Left + textBounds.Width / 2, textBounds.Top + textBounds.Height / 2);
        if (exclusion.Contains(center)) return true;
        RectangleF overlap = RectangleF.Intersect(textBounds, exclusion);
        return overlap.Width * overlap.Height >= textBounds.Width * textBounds.Height * .35f;
    }
}
