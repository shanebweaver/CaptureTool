using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.Abstractions.RecentCaptures;

public interface IRecentCapture
{
    string FilePath { get; }
    string FileName { get; }
    CaptureFileType CaptureFileType { get; }
}
