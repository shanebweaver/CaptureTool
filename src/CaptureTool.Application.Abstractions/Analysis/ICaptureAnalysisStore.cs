using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>Application features read normalized metadata, never provider responses or storage files.</summary>
public interface ICaptureMetadataReader
{
    /// <summary>Returns no record when a supplied source revision differs from the stored revision.</summary>
    Task<CaptureAnalysisRecord?> GetAsync(CaptureId captureId, SourceRevision? expectedSource = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CaptureAnalysisRecord>> ReadAllAsync(CancellationToken cancellationToken = default);
}

public sealed record AnalysisWriteToken(CaptureId CaptureId, SourceRevision SourceRevision, Guid Generation, Guid RunId);
public sealed record AnalysisCleanupResult(bool Completed, int RemainingGenerations);

/// <summary>Singleton persistence boundary. Tokens fence concurrent refreshes, source changes, and clear.</summary>
public interface ICaptureAnalysisStore : ICaptureMetadataReader
{
    Task<AnalysisCleanupResult> InitializeAsync(CancellationToken cancellationToken = default);

    Task<AnalysisWriteToken> BeginRunAsync(CaptureId captureId, AnalysisMediaKind mediaKind,
        SourceRevision sourceRevision, string planVersion, CancellationToken cancellationToken = default);

    /// <summary>False means the token was superseded. IO/protection/schema errors are not successful commits.</summary>
    Task<bool> TryWriteAsync(AnalysisWriteToken token, AnalysisResult result, CancellationToken cancellationToken = default);

    /// <summary>Invalidates previous tokens before cleanup. Incomplete cleanup remains retryable at initialization.</summary>
    Task<AnalysisCleanupResult> ClearAsync(CancellationToken cancellationToken = default);
}
