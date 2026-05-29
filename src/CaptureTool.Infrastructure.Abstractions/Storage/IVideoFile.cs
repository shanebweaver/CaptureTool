namespace CaptureTool.Infrastructure.Abstractions.Storage;

public partial interface IVideoFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Video;
}