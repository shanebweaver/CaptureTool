using System.Drawing;

namespace CaptureTool.Domain.Edit;

public readonly record struct ImageEditRenderSnapshot(
    ImageOrientation Orientation,
    Size ImageSize,
    Rectangle CropRect);
