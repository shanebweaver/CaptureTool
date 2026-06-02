using CaptureTool.Domain.Capture.Abstractions.Files;

namespace CaptureTool.Domain.Capture.Abstractions;

public sealed partial class AudioFile : FileBase, IAudioFile
{
    public AudioFile(string path) : base(path) { }
}
