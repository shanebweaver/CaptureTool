using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces;

public interface IFileTypeDetector
{
    CaptureFileType DetectFileType(string filePath);
    bool IsImageFile(string filePath);
    bool IsVideoFile(string filePath);
    bool IsAudioFile(string filePath);
}
