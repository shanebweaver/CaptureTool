namespace CaptureTool.Application.Abstractions.Features.VideoEdit.SaveVideoFile;

public sealed record SaveVideoFileRequest(string VideoPath, TimeSpan? TrimStart = null, TimeSpan? TrimEnd = null);
