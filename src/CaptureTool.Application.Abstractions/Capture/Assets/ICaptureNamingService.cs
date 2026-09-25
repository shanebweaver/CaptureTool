using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

public sealed record CaptureNamingState(CaptureName? Name, bool IsGenerating);
public sealed record CaptureFileRename(string OldPath, string NewPath);

public interface ICaptureNamingService
{
    event Action? Changed;
    Task<CaptureName?> GetNameAsync(string path, CancellationToken cancellationToken = default);
    Task<CaptureNamingState> GetStateAsync(string path, CancellationToken cancellationToken = default);
    Task<CaptureFileRename?> RenameAsync(string path, CaptureFileType mediaType, string name, CancellationToken cancellationToken = default);
}
