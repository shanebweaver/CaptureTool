using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

public sealed record CaptureRegistration(CaptureAsset Asset, long Sequence, Guid? AutomaticAuthorization, Guid? NamingEpoch = null);

/// <summary>Durable asset identity, independent of recent history. Never deletes source files.</summary>
public interface ICaptureAssetCatalog
{
    Task RegisterAsync(CaptureAsset asset, CancellationToken cancellationToken = default);
    Task RegisterForAnalysisAsync(CaptureAsset asset, Guid? automaticAuthorization, CancellationToken cancellationToken = default, Guid? namingEpoch = null);
    Task<IReadOnlyList<CaptureRegistration>> ReadRegistrationsAsync(CancellationToken cancellationToken = default);
    Task<long> GetBoundaryAsync(CancellationToken cancellationToken = default);
    Task<CaptureAsset?> GetAsync(CaptureId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CaptureAsset>> ReadAllAsync(CancellationToken cancellationToken = default);
    Task SetPreferredPathAsync(CaptureId id, string? path, CancellationToken cancellationToken = default);
    Task RelocateSourceAsync(CaptureId id, string path, CancellationToken cancellationToken = default);
}
