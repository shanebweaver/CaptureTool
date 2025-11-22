namespace CaptureTool.Services.Interfaces.Storage;

public partial interface IImageFile : IFile
{
    public FileType FileType => FileType.Image;
}
