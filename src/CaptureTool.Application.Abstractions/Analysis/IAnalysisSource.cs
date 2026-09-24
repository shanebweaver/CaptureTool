using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

public interface IAnalysisSource
{
    Task<IAnalysisSourceLease> OpenAsync(string path, CancellationToken cancellationToken);
}

/// <summary>Keeps the source immutable while adapters read it, and verifies bytes again before publication.</summary>
public interface IAnalysisSourceLease : IAsyncDisposable
{
    string Path { get; }
    SourceRevision Revision { get; }
    Task<bool> VerifyAsync(CancellationToken cancellationToken);
}
