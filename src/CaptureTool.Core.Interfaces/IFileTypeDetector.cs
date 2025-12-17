using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Interfaces;

public interface IFileTypeDetector
{
    CaptureFileType DetectFileType(string filePath);
    bool IsImageFile(string filePath);
    bool IsVideoFile(string filePath);
}
