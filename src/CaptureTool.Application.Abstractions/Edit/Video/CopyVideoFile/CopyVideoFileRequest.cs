namespace CaptureTool.Application.Abstractions.Edit.Video.CopyVideoFile;

public sealed record CopyVideoFileRequest(string VideoPath, TimeSpan? TrimStart = null, TimeSpan? TrimEnd = null);
