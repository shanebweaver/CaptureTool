using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Domain.Capture;

public sealed partial class ImageFile : FileBase, IImageFile
{
    public ImageFile(string path) : base(path) { }
}
