using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public interface IRecognizedTextDocumentBuilder
{
    /// <summary>Null regions/codes mean that scan is missing; an empty collection is a completed empty scan.</summary>
    RecognizedTextDocument Build(Size imageSize, IReadOnlyList<RecognizedTextRegion>? regions,
        IReadOnlyList<RecognizedQrCodeRegion>? qrCodes);
}
