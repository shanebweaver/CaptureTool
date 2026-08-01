namespace CaptureTool.Domain.FileSystem;

public sealed partial class ImageFile : FileReference
{
    public string? PersistentFilePath { get; set; }

    public ImageFile(string path, string? persistentFilePath = null) : base(path)
    {
        PersistentFilePath = persistentFilePath;
    }
}
