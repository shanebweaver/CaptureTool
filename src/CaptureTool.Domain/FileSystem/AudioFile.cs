namespace CaptureTool.Domain.FileSystem;

public sealed partial class AudioFile : FileReference
{
    public string? PersistentFilePath { get; set; }
    public AudioFile(string path, string? persistentFilePath = null) : base(path) => PersistentFilePath = persistentFilePath;
}
