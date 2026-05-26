using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.AppMenu;

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
