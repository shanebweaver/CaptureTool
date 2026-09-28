using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain.Analysis.Payloads;
using System.Drawing;

namespace CaptureTool.Presentation.Features.CaptureDetails;

/// <summary>Projects verified saved bounds into the existing text-selection renderer.</summary>
public sealed record CaptureImageTextOverlay(IReadOnlyList<RecognizedTextRegion> Text, IReadOnlyList<RecognizedQrCodeRegion> QrCodes)
{
    public static IReadOnlyList<RecognizedTextRegion> CreateSelectionRegions(CaptureTextPassage passage, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0 || passage.SelectedLocation is not { Time: null, Bounds: { Width: > 0, Height: > 0 } bounds })
            return [];
        if (passage.Source == CaptureTextSource.QrCode)
            return [new(passage.Text, Pixels(bounds, size))];
        return Create([passage], size).Text;
    }

    public static CaptureImageTextOverlay Create(IReadOnlyList<CaptureTextPassage> passages, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0) return new([], []);
        List<RecognizedTextRegion> text = [];
        List<RecognizedQrCodeRegion> qr = [];
        int paragraphIndex = 0;
        foreach (var passage in passages)
        {
            if (passage.Source == CaptureTextSource.ImageText && passage.TextLines.Count > 0)
            {
                for (int lineIndex = 0; lineIndex < passage.TextLines.Count; lineIndex++)
                {
                    var line = passage.TextLines[lineIndex];
                    for (int wordIndex = 0; wordIndex < line.Count; wordIndex++)
                    {
                        var region = line[wordIndex];
                        if (region.Timestamp == null && region.Bounds is { Width: > 0, Height: > 0 } bounds)
                            text.Add(new(region.Text, Pixels(bounds, size), lineIndex, wordIndex, paragraphIndex));
                    }
                }
                paragraphIndex++;
                continue;
            }
            if (passage.Source == CaptureTextSource.ImageText && passage.TextRegions.Count > 0)
            {
                foreach (var region in passage.TextRegions)
                    if (region.Timestamp == null && region.Bounds is { Width: > 0, Height: > 0 } bounds)
                        text.Add(new(region.Text, Pixels(bounds, size), region.LineIndex ?? -1, region.WordIndex ?? -1));
                continue;
            }
            foreach (var location in passage.Locations)
            {
                if (location.Time != null || location.Bounds is not { Width: > 0, Height: > 0 } bounds) continue;
                if (passage.Source == CaptureTextSource.ImageText) text.Add(new(passage.Text, Pixels(bounds, size)));
                else if (passage.Source == CaptureTextSource.QrCode) qr.Add(new(passage.Text, Pixels(bounds, size)));
            }
        }
        return new(text, qr);
    }

    private static RectangleF Pixels(NormalizedBounds bounds, Size size) => new((float)(bounds.X * size.Width), (float)(bounds.Y * size.Height),
        (float)(bounds.Width * size.Width), (float)(bounds.Height * size.Height));
}
