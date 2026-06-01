namespace CaptureTool.Application.Features.VideoEdit.CopyVideoFile;

public sealed record CopyVideoFileRequest(string VideoPath, TimeSpan? TrimStart = null, TimeSpan? TrimEnd = null);
