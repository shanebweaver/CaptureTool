using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Capture;

public sealed partial class VideoFile : FileBase, IVideoFile
{
    public VideoFile(string path) : base(path) { }
}
