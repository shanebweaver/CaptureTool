namespace CaptureTool.Domain.Files;

public partial interface IAudioFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Audio;
}
