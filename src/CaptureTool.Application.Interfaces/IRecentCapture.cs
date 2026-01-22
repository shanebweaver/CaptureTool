using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces;

public interface IRecentCapture
{
    string FilePath { get; }
    string FileName { get; }
    CaptureFileType CaptureFileType { get; }
}
