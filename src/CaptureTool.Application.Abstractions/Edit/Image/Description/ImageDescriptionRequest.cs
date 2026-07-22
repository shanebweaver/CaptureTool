using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.Description;

public sealed record ImageDescriptionRequest(
    Stream SourceImage,
    Size SourceSize,
    ImageDescriptionMode Mode);
