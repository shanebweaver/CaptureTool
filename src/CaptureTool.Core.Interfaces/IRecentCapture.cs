using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Interfaces;

public interface IRecentCapture
{
    string FilePath { get; }
    string FileName { get; }
    CaptureFileType CaptureFileType { get; }
}
