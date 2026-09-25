namespace CaptureTool.Domain.FileSystem;

public partial class VideoFile : FileReference
{
    public string? PersistentFilePath { get; set; }
    public VideoFile(string path, string? persistentFilePath = null) : base(path) => PersistentFilePath = persistentFilePath;
}
