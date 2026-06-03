using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Domain.Capture;

public partial class VideoFile : FileBase, IVideoFile
{
    public VideoFile(string path) : base(path) { }
}
