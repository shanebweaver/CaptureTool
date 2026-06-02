namespace CaptureTool.Domain.Capture.Abstractions.Files;

public partial interface IVideoFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Video;
}
