using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.Sources;

internal sealed class LocalAnalysisSource : IAnalysisSource
{
    // Reads are streaming; this cap bounds hashing time and admission of unexpectedly large sources.
    private const long MaximumSourceBytes = 4L * 1024 * 1024 * 1024;

    public async Task<IAnalysisSourceLease> OpenAsync(string path, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Source path must be absolute.", nameof(path));
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            if (stream.Length == 0 || stream.Length > MaximumSourceBytes) throw new InvalidDataException("Source size is unsupported.");
            SourceRevision revision = await HashAsync(stream, cancellationToken).ConfigureAwait(false);
            return new Lease(path, stream, revision);
        }
        catch { await stream.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private static async Task<SourceRevision> HashAsync(FileStream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        return new(Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)));
    }

    private sealed class Lease(string path, FileStream stream, SourceRevision revision) : IAnalysisSourceLease
    {
        public string Path => path;
        public SourceRevision Revision => revision;
        public async Task<bool> VerifyAsync(CancellationToken cancellationToken) =>
            await HashAsync(stream, cancellationToken).ConfigureAwait(false) == Revision;
        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }
}
