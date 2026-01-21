namespace CaptureTool.Infrastructure.Interfaces.Storage;

public partial interface IImageFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Image;
}
