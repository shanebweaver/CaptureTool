namespace CaptureTool.Domains.Edit.Interfaces;

/// <summary>
/// Specifies how much an image is rotated and the axis used to flip the image.
/// </summary>
public enum ImageOrientation
{
    RotateNoneFlipNone = 0,
    Rotate90FlipNone = 1,
    Rotate180FlipNone = 2,
    Rotate270FlipNone = 3,
    RotateNoneFlipX = 4,
    Rotate90FlipX = 5,
    Rotate180FlipX = 6,
    Rotate270FlipX = 7,
}