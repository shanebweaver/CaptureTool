using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record RecognizedTextRegion(
    string Text,
    RectangleF Bounds,
    int LineIndex = -1,
    int WordIndex = -1);

