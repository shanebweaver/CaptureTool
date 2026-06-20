using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Domain.Capture;

public sealed partial class ImageFile : FileBase, IImageFile
{
    public ImageFile(string path) : base(path) { }

    public string? AutoSavedFilePath { get; private set; }

    public void MarkAutoSavedAs(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Auto-saved file path cannot be empty.", nameof(filePath));
        }

        AutoSavedFilePath = filePath;
    }
}
