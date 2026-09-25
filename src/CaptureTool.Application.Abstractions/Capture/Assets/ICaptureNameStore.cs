using CaptureTool.Domain;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

public sealed record CaptureNamingSnapshot(Guid? Epoch, IReadOnlyList<CaptureRegistration> Pending);

/// <summary>Names and enrollment share the protected asset catalog's atomic publication boundary.</summary>
public interface ICaptureNameStore
{
    Task<CaptureNamingSnapshot> ReadNamingAsync(CancellationToken cancellationToken = default);
    Task SetAutomaticNamingAsync(bool enabled, CancellationToken cancellationToken = default);
    Task InvalidatePendingNamesAsync(CancellationToken cancellationToken = default);
    Task<bool> TryApplyAutomaticNameAsync(CaptureId id, string name, Guid epoch, string expectedSourcePath, CancellationToken cancellationToken = default);
    Task SetUserNameAsync(CaptureId id, string name, CancellationToken cancellationToken = default);
}
