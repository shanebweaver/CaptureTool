namespace CaptureTool.Infrastructure.Interfaces.Storage;

public partial interface IAudioFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Audio;
}
