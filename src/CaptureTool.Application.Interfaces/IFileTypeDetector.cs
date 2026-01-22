using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces;

public interface IFileTypeDetector
{
    CaptureFileType DetectFileType(string filePath);
    bool IsImageFile(string filePath);
    bool IsVideoFile(string filePath);
}
