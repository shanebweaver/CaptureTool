using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using System.Drawing;

namespace CaptureTool.Application.Edit.Image.TextExtraction;

/// <summary>Shared reading order, QR exclusion, and copy text for saved and freshly scanned results.</summary>
public sealed class RecognizedTextDocumentBuilder : IRecognizedTextDocumentBuilder
{
    public RecognizedTextDocument Build(Size imageSize, IReadOnlyList<RecognizedTextRegion>? regions,
        IReadOnlyList<RecognizedQrCodeRegion>? qrCodes)
    {
        var retained = regions?.Where(region => !string.IsNullOrWhiteSpace(region.Text) &&
            qrCodes?.Any(code => code.OverlapsText(region.Bounds)) != true).ToArray() ?? [];
        var layout = RecognizedTextLayout.Create(retained, includeCutoutContours: false);
        string text = retained.Length != 0 && layout.ReadingOrder.Count == retained.Length
            ? layout.Select(0, layout.ReadingOrder.Count - 1).Text
            : string.Join(" ", retained.Select(region => region.Text));
        return RecognizedTextDocument.FromRecognition(text, imageSize, retained, qrCodes, regions != null);
    }
}
