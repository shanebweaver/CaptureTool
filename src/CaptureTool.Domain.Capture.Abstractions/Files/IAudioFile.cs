namespace CaptureTool.Domain.Capture.Abstractions.Files;

public partial interface IAudioFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Audio;
}
