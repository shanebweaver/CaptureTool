using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Files;

public interface IFileTypeDetector
{
    CaptureFileType DetectFileType(string filePath);
    bool IsImageFile(string filePath);
    bool IsVideoFile(string filePath);
    bool IsAudioFile(string filePath);
}
