using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;

public sealed record ForegroundExtractionRequest(
    ImageFile SourceImage,
    Size SourceSize,
    Point ForegroundPoint);
