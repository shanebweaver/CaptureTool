namespace CaptureTool.Services.Interfaces.Storage;

public partial interface IVideoFile : IFile
{
    public FileType FileType => FileType.Video;
}