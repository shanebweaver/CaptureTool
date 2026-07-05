namespace CaptureTool.Domain.Files;

public sealed partial class AudioFile : FileReference
{
    public AudioFile(string path) : base(path) { }
}
