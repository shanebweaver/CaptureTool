namespace CaptureTool.Domain.Capture.Files;

public partial interface IVideoFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Video;
}
