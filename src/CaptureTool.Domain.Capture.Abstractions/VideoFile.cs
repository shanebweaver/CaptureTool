using CaptureTool.Domain.Capture.Abstractions.Files;

namespace CaptureTool.Domain.Capture.Abstractions;

public partial class VideoFile : FileBase, IVideoFile
{
    public VideoFile(string path) : base(path) { }
}
