namespace CaptureTool.Domain.Capture.Files;

public partial interface IImageFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Image;
}
