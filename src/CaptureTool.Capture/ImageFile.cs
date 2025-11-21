using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Capture;

public sealed partial class ImageFile : FileBase, IImageFile
{
    public ImageFile(string path) : base(path) { }
}
