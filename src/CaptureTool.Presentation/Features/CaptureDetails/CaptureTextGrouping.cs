using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Analysis.TextLayout;

namespace CaptureTool.Presentation.Features.CaptureDetails;

/// <summary>Projects the shared layout into passages without modifying OCR evidence.</summary>
internal static class CaptureTextGrouping
{
    internal sealed record Group(int FirstIndex, string Text, NormalizedBounds? Bounds,
        IReadOnlyList<RecognizedText> Regions, IReadOnlyList<IReadOnlyList<RecognizedText>> Lines);

    public static IReadOnlyList<Group> Create(IReadOnlyList<RecognizedText> regions) =>
        RecognizedTextGrouping.Create(regions).Select(paragraph =>
        {
            var lines = paragraph.Lines.Select(line =>
                (IReadOnlyList<RecognizedText>)line.WordIndices.Select(index => regions[index]).ToArray()).ToArray();
            return new Group(paragraph.FirstIndex, paragraph.Text, paragraph.Bounds,
                lines.SelectMany(line => line).ToArray(), lines);
        }).ToArray();
}
