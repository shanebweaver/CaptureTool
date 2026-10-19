using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>Filesystem identity of the selected file, independent of its name and contents.</summary>
public sealed record AnalysisSourceIdentity(ulong VolumeId, ulong FileIdLow, ulong FileIdHigh);

public interface IAnalysisSource
{
    /// <summary>Reads only file identity; does not hash content or hold the file open while queued.</summary>
    Task<AnalysisSourceIdentity> GetIdentityAsync(string path, CancellationToken cancellationToken);
    Task<IAnalysisSourceLease> OpenAsync(string path, CancellationToken cancellationToken, AnalysisSourceIdentity? expectedIdentity = null);
}

/// <summary>Keeps the source immutable while adapters read it, and verifies bytes again before publication.</summary>
public interface IAnalysisSourceLease : IAsyncDisposable
{
    string Path { get; }
    SourceRevision Revision { get; }
    Task<bool> VerifyAsync(CancellationToken cancellationToken);
}
