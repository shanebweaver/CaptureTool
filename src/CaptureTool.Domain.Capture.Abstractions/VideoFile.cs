using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domain.Capture.Abstractions;

public partial class VideoFile : FileBase, IVideoFile
{
    public VideoFile(string path) : base(path) { }
}
