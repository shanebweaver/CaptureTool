namespace CaptureTool.Domain.Capture.Files;

public partial interface IAudioFile : IFile
{
    FilePickerType FilePickerType => FilePickerType.Audio;
}
