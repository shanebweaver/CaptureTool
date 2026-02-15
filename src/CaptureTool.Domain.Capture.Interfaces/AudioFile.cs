using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domain.Capture.Interfaces;

public sealed partial class AudioFile : FileBase, IAudioFile
{
    public AudioFile(string path) : base(path) { }
}
