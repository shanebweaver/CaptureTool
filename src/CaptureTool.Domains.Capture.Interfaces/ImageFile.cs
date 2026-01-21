using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

public sealed partial class ImageFile : FileBase, IImageFile
{
    public ImageFile(string path) : base(path) { }
}
