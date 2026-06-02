using CaptureTool.Domain.Capture.Abstractions.Files;

namespace CaptureTool.Domain.Capture.Abstractions;

public sealed partial class ImageFile : FileBase, IImageFile
{
    public ImageFile(string path) : base(path) { }
}
