namespace CaptureTool.Services.Interfaces.Storage;

public partial interface IVideoFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Video;
}