using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;

public sealed record ImageSuperResolutionRequest(
    ImageFile SourceImage,
    Size SourceSize,
    double ScaleFactor = 2.0);
