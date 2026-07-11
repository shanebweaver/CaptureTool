using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record RecognizedTextDocument(
    string Text,
    Size ImageSize,
    IReadOnlyList<RecognizedTextRegion> Regions);

