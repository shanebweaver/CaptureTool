using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

/// <summary>Durable asset identity, independent of recent history. Never deletes source files.</summary>
public interface ICaptureAssetCatalog
{
    Task RegisterAsync(CaptureAsset asset, CancellationToken cancellationToken = default);
    Task<CaptureAsset?> GetAsync(CaptureId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CaptureAsset>> ReadAllAsync(CancellationToken cancellationToken = default);
    Task SetPreferredPathAsync(CaptureId id, string? path, CancellationToken cancellationToken = default);
    Task RelocateSourceAsync(CaptureId id, string path, CancellationToken cancellationToken = default);
}
