using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;

public sealed record ObjectEraseRequest(
    ImageFile SourceImage,
    Size SourceSize,
    Point ObjectPoint);
