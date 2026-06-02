using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Domain.Capture;

public sealed partial class AudioFile : FileBase, IAudioFile
{
    public AudioFile(string path) : base(path) { }
}
