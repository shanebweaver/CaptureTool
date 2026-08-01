using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures;

public sealed record RecentCaptureCatalogEntry(
    string FilePath,
    CaptureFileType CaptureFileType,
    RecentCaptureOrigin Origin,
    DateTime LastActivityUtc);
