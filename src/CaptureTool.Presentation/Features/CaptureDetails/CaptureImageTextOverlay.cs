using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using System.Drawing;

namespace CaptureTool.Presentation.Features.CaptureDetails;

/// <summary>Projects verified saved bounds into the existing text-selection renderer.</summary>
public sealed record CaptureImageTextOverlay(IReadOnlyList<RecognizedTextRegion> Text, IReadOnlyList<RecognizedQrCodeRegion> QrCodes)
{
    public static CaptureImageTextOverlay Create(IReadOnlyList<CaptureTextPassage> passages, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0) return new([], []);
        List<RecognizedTextRegion> text = [];
        List<RecognizedQrCodeRegion> qr = [];
        foreach (var passage in passages)
        foreach (var location in passage.Locations)
        {
            if (location.Time != null || location.Bounds is not { Width: > 0, Height: > 0 } bounds) continue;
            var pixels = new RectangleF((float)(bounds.X * size.Width), (float)(bounds.Y * size.Height),
                (float)(bounds.Width * size.Width), (float)(bounds.Height * size.Height));
            if (passage.Source == CaptureTextSource.ImageText) text.Add(new(passage.Text, pixels));
            else if (passage.Source == CaptureTextSource.QrCode) qr.Add(new(passage.Text, pixels));
        }
        return new(text, qr);
    }
}
