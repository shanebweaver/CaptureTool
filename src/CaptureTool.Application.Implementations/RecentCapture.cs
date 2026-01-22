using CaptureTool.Application.Interfaces;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Application.Implementations;

public sealed class RecentCapture : IRecentCapture
{
    public string FilePath { get; }
    public string FileName { get; }
    public CaptureFileType CaptureFileType { get; }

    public RecentCapture(string filePath, string fileName, CaptureFileType captureFileType)
    {
        FilePath = filePath;
        FileName = fileName;
        CaptureFileType = captureFileType;
    }
}
