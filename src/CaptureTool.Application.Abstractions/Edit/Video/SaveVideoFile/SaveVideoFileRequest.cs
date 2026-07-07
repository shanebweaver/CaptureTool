namespace CaptureTool.Application.Abstractions.Edit.Video.SaveVideoFile;

public sealed record SaveVideoFileRequest(string VideoPath, TimeSpan? TrimStart = null, TimeSpan? TrimEnd = null);
