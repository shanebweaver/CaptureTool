using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

public sealed partial class VideoFile : FileBase, IVideoFile
{
    public VideoFile(string path) : base(path) { }
}
