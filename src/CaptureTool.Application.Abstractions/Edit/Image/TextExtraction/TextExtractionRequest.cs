using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record TextExtractionRequest(
    Stream SourceImage,
    Size SourceSize);

