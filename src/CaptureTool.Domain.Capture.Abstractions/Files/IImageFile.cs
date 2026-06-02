namespace CaptureTool.Domain.Capture.Abstractions.Files;

public partial interface IImageFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Image;
}
