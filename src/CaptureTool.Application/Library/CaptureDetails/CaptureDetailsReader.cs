using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.CaptureDetails;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using System.Security.Cryptography;

namespace CaptureTool.Application.Library.CaptureDetails;

public sealed class CaptureDetailsReader(ICaptureAssetCatalog catalog, ICaptureMetadataReader metadata,
    IAnalysisExecutionStore execution, IAnalysisSource files, IMediaFileDetailsReader? properties = null) : ICaptureDetailsReader
{
    public async Task<FileDetailsMetadata?> ReadFileAsync(string path, AnalysisMediaKind kind, CancellationToken cancellationToken = default)
    {
        if (properties == null || !Path.IsPathFullyQualified(path)) return null;
        DateTimeOffset? capturedAt = null;
        try
        {
            var matches = (await catalog.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                .Where(asset => SamePath(path, asset.SourcePath) || SamePath(path, asset.PreferredPath)).Take(2).ToArray();
            if (matches.Length == 1) capturedAt = matches[0].CapturedAt;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // An unavailable catalog must not hide readable filesystem properties.
        }
        return await properties.ReadAsync(path, kind, capturedAt, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CaptureDetailsSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathFullyQualified(path)) return new(CaptureDetailsStatus.SourceUnavailable);
        try
        {
            var scope = await execution.GetAdmissionScopeAsync(cancellationToken).ConfigureAwait(false);
            CaptureAsset[] matches = (await catalog.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                .Where(asset => SamePath(path, asset.SourcePath) || SamePath(path, asset.PreferredPath)).Take(2).ToArray();
            // Ambiguous paths must not expose another capture's metadata.
            if (matches.Length != 1) return new(CaptureDetailsStatus.Empty);
            CaptureAsset asset = matches[0];
            AnalysisRun? run = (await execution.GetWorkAsync(asset.Id, cancellationToken).ConfigureAwait(false))?.Run;
            CaptureAnalysisRecord? record = await metadata.GetAsync(asset.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
            CaptureDetailsSnapshot snapshot;
            try
            {
                await using var lease = await files.OpenAsync(path, cancellationToken).ConfigureAwait(false);
                if (record != null && (record.CaptureId != asset.Id || record.MediaKind != MediaKind(asset.MediaType) ||
                    record.SourceRevision != lease.Revision) || !await lease.VerifyAsync(cancellationToken).ConfigureAwait(false))
                    snapshot = new(CaptureDetailsStatus.SourceChanged);
                else
                {
                    if (run?.SourceRevision is { } revision && revision != lease.Revision) run = null;
                    snapshot = new(record?.Results.Count > 0 ? CaptureDetailsStatus.Available : CaptureDetailsStatus.Empty, record, run);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                snapshot = new(CaptureDetailsStatus.SourceUnavailable);
            }
            // Deletion/recovery during a read invalidates the entire snapshot, including its execution state.
            if (scope.Generation != (await execution.GetAdmissionScopeAsync(cancellationToken).ConfigureAwait(false)).Generation)
                return new(CaptureDetailsStatus.Empty);
            return snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new(CaptureDetailsStatus.Unavailable);
        }
    }

    private static bool SamePath(string first, string? second) => second != null && Path.IsPathFullyQualified(second) &&
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    private static AnalysisMediaKind MediaKind(CaptureFileType type) => type switch
    {
        CaptureFileType.Image => AnalysisMediaKind.Image,
        CaptureFileType.Audio => AnalysisMediaKind.Audio,
        CaptureFileType.Video => AnalysisMediaKind.Video,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
