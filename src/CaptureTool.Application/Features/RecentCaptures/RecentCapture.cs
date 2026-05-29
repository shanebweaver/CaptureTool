using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.Features.RecentCaptures;

public sealed record RecentCapture(
    string FilePath,
    string FileName,
    CaptureFileType CaptureFileType);
