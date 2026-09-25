using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

public interface ICaptureNamingService
{
    bool IsEnabled { get; }
    bool IsAvailable { get; }
    event Action? Changed;
    Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<CaptureName?> GetNameAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> SetNameAsync(string path, CaptureFileType mediaType, string name, CancellationToken cancellationToken = default);
}
