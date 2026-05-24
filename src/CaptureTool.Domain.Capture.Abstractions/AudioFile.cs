using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Domain.Capture.Abstractions;

public sealed partial class AudioFile : FileBase, IAudioFile
{
    public AudioFile(string path) : base(path) { }
}
