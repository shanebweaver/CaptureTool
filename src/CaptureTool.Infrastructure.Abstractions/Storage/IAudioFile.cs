namespace CaptureTool.Infrastructure.Abstractions.Storage;

public partial interface IAudioFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Audio;
}
