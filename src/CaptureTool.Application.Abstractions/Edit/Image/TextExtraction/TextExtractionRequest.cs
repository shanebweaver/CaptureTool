using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record TextExtractionRequest(
    ImageFile SourceImage,
    Size SourceSize);

