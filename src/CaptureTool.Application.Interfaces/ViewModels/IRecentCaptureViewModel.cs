using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IRecentCaptureViewModel
{
    string FilePath { get; }
    string FileName { get; }
    CaptureFileType CaptureFileType { get; }
}
