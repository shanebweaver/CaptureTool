namespace CaptureTool.Domain.Files;

public partial interface IImageFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Image;
}
