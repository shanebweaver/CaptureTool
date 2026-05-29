namespace CaptureTool.Infrastructure.Abstractions.Storage;

public partial interface IImageFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Image;
}
