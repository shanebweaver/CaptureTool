namespace CaptureTool.Domain.Files;

public partial interface IVideoFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Video;
}
