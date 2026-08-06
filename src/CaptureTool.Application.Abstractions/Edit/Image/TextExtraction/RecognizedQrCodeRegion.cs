using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record RecognizedQrCodeRegion(
    string Value,
    RectangleF Bounds);
