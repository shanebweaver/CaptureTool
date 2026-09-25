using CaptureTool.Domain;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

public sealed record CaptureNamingSnapshot(Guid? Epoch, IReadOnlyList<CaptureRegistration> Pending);

/// <summary>Names and enrollment share the protected asset catalog's atomic publication boundary.</summary>
public interface ICaptureNameStore
{
    Task<CaptureNamingSnapshot> ReadNamingAsync(CancellationToken cancellationToken = default);
    Task SetAutomaticNamingAsync(bool enabled, CancellationToken cancellationToken = default);
    Task EnableAutomaticNamingByDefaultAsync(CancellationToken cancellationToken = default);
    Task InvalidatePendingNamesAsync(CancellationToken cancellationToken = default);
    /// <summary>Saves a pending suggestion; never changes a file or its displayed name.</summary>
    Task<bool> TryApplyAutomaticNameAsync(CaptureId id, string name, Guid epoch, string expectedSourcePath, CancellationToken cancellationToken = default);
    /// <summary>Renames one registered file without overwriting another file, and commits its new identity path.</summary>
    Task<string> RenameFileAsync(CaptureId id, string path, string fileName, CancellationToken cancellationToken = default);
}
