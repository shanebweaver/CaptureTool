namespace CaptureTool.Infrastructure.Interfaces.Storage;

public partial interface IVideoFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Video;
}