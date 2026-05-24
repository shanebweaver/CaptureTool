using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Domain.Capture.Abstractions;

public sealed partial class ImageFile : FileBase, IImageFile
{
    public ImageFile(string path) : base(path) { }
}
