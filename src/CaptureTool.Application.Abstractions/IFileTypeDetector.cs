using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.Abstractions;

public interface IFileTypeDetector
{
    CaptureFileType DetectFileType(string filePath);
    bool IsImageFile(string filePath);
    bool IsVideoFile(string filePath);
    bool IsAudioFile(string filePath);
}
