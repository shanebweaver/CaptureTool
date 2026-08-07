using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Persistence.Serialization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Persistence;

internal sealed class LocalCaptureAnalysisStore : ICaptureAnalysisStore, IDisposable
{
    internal const string AnalysisDirectoryName = "CaptureAnalysis";
    internal const string MetadataVersionDirectoryName = "metadata-v1";
    internal const string EnvelopeDirectoryName = "envelopes";
    internal const string QuarantineDirectoryName = "quarantine";
    internal const string EnvelopeExtension = ".analysis";
    internal const int CurrentSchemaVersion = 1;

    private const string FileNameDomain = "capture-analysis-envelope/v1/";

    private readonly IApplicationLocalCachePathProvider _localCachePathProvider;
    private readonly IUserDataProtectionService _dataProtectionService;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalCaptureAnalysisStore(
        IApplicationLocalCachePathProvider localCachePathProvider,
        IUserDataProtectionService dataProtectionService,
        IAtomicFileWriter atomicFileWriter,
        ILogService logService)
    {
        _localCachePathProvider = localCachePathProvider;
        _dataProtectionService = dataProtectionService;
        _atomicFileWriter = atomicFileWriter;
        _logService = logService;
    }

    public async ValueTask<CaptureAnalysisStoreSnapshot?> GetAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Metadata lookup requires a capture ID.", nameof(captureId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnvelopeLoadResult loaded = LoadEnvelope(GetEnvelopeFilePath(captureId), captureId);
            return loaded.Status == EnvelopeLoadStatus.Known
                ? loaded.Envelope!.Snapshot
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async IAsyncEnumerable<CaptureAnalysisStoreSnapshot> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<CaptureAnalysisStoreSnapshot> snapshots = [];
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string directoryPath = GetEnvelopeDirectoryPath();
            if (Directory.Exists(directoryPath))
            {
                foreach (string filePath in Directory.EnumerateFiles(
                    directoryPath,
                    $"*{EnvelopeExtension}",
                    SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnvelopeLoadResult loaded = LoadEnvelope(filePath, expectedCaptureId: null);
                    if (loaded.Status == EnvelopeLoadStatus.Known)
                    {
                        snapshots.Add(loaded.Envelope!.Snapshot);
                    }
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        foreach (CaptureAnalysisStoreSnapshot snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return snapshot;
        }
    }

    internal async ValueTask<CaptureAnalysisStoreWriteResult> TryWriteAsync(
        CaptureAnalysisRecord record,
        long? expectedDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (expectedDocumentRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedDocumentRevision));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string filePath = GetEnvelopeFilePath(record.CaptureId);
            EnvelopeLoadResult loaded = LoadEnvelope(filePath, record.CaptureId);
            if (loaded.Status == EnvelopeLoadStatus.ReadOnlyVersion)
            {
                return new(CaptureAnalysisStoreWriteStatus.ReadOnlyVersion);
            }

            if (loaded.Status == EnvelopeLoadStatus.Corrupt)
            {
                return new(CaptureAnalysisStoreWriteStatus.Unavailable);
            }

            if (loaded.Status == EnvelopeLoadStatus.Missing && expectedDocumentRevision.HasValue)
            {
                return new(CaptureAnalysisStoreWriteStatus.NotFound);
            }

            if (loaded.Status == EnvelopeLoadStatus.Known)
            {
                CaptureAnalysisStoreSnapshot current = loaded.Envelope!.Snapshot;
                if (!expectedDocumentRevision.HasValue ||
                    current.DocumentRevision != expectedDocumentRevision.Value)
                {
                    return new(CaptureAnalysisStoreWriteStatus.Conflict, current);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            long nextRevision;
            try
            {
                nextRevision = loaded.Status == EnvelopeLoadStatus.Known
                    ? checked(loaded.Envelope!.Snapshot.DocumentRevision + 1)
                    : 1;
            }
            catch (OverflowException ex)
            {
                _logService.LogException(ex, "Capture Analysis envelope revision is exhausted.");
                return new(CaptureAnalysisStoreWriteStatus.Unavailable);
            }

            byte[] plaintext = CaptureAnalysisDocumentSerializer.SerializeEnvelope(
                record,
                nextRevision,
                CurrentSchemaVersion,
                loaded.Envelope?.OpaqueCapabilityEntries);
            try
            {
                byte[] protectedBytes = _dataProtectionService.Protect(plaintext);
                _atomicFileWriter.Write(filePath, protectedBytes);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Failed to persist a Capture Analysis envelope.");
                return new(CaptureAnalysisStoreWriteStatus.Unavailable);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            var committed = new CaptureAnalysisStoreSnapshot(nextRevision, record);
            return new(CaptureAnalysisStoreWriteStatus.Succeeded, committed);
        }
        finally
        {
            _gate.Release();
        }
    }

    // This is the raw derived-data cleanup primitive. Conditional mutation coordination verifies
    // and durably advances the control ledger before invoking it.
    internal async ValueTask<CaptureAnalysisStoreWriteResult> TryDeleteAsync(
        CaptureId captureId,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Metadata deletion requires a capture ID.", nameof(captureId));
        }

        if (expectedDocumentRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedDocumentRevision));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string filePath = GetEnvelopeFilePath(captureId);
            EnvelopeLoadResult loaded = LoadEnvelope(filePath, captureId);
            if (loaded.Status == EnvelopeLoadStatus.Missing)
            {
                return new(CaptureAnalysisStoreWriteStatus.NotFound);
            }

            if (loaded.Status == EnvelopeLoadStatus.ReadOnlyVersion)
            {
                return new(CaptureAnalysisStoreWriteStatus.ReadOnlyVersion);
            }

            if (loaded.Status == EnvelopeLoadStatus.Corrupt)
            {
                return new(CaptureAnalysisStoreWriteStatus.Unavailable);
            }

            CaptureAnalysisStoreSnapshot current = loaded.Envelope!.Snapshot;
            if (current.DocumentRevision != expectedDocumentRevision)
            {
                return new(CaptureAnalysisStoreWriteStatus.Conflict, current);
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Delete(filePath);
                return new(CaptureAnalysisStoreWriteStatus.Succeeded, current);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Failed to delete a Capture Analysis envelope.");
                return new(CaptureAnalysisStoreWriteStatus.Unavailable);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    internal string GetEnvelopeFilePath(CaptureId captureId)
    {
        return Path.Combine(GetEnvelopeDirectoryPath(), GetOpaqueFileName(captureId));
    }

    internal static string GetOpaqueFileName(CaptureId captureId)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("An opaque filename requires a capture ID.", nameof(captureId));
        }

        byte[] input = Encoding.UTF8.GetBytes(FileNameDomain + captureId);
        byte[] hash = SHA256.HashData(input);
        return Convert.ToHexStringLower(hash) + EnvelopeExtension;
    }

    private string GetEnvelopeDirectoryPath()
    {
        return Path.Combine(
            _localCachePathProvider.GetApplicationLocalCacheFolderPath(),
            AnalysisDirectoryName,
            MetadataVersionDirectoryName,
            EnvelopeDirectoryName);
    }

    private EnvelopeLoadResult LoadEnvelope(string filePath, CaptureId? expectedCaptureId)
    {
        if (!File.Exists(filePath))
        {
            return EnvelopeLoadResult.Missing;
        }

        byte[]? plaintext = null;
        try
        {
            byte[] protectedBytes = File.ReadAllBytes(filePath);
            plaintext = _dataProtectionService.Unprotect(protectedBytes);
            CaptureAnalysisDocumentHeader header =
                CaptureAnalysisDocumentSerializer.ReadHeader(plaintext);
            if (header.SchemaVersion != CurrentSchemaVersion)
            {
                return EnvelopeLoadResult.ReadOnly;
            }

            CaptureAnalysisEnvelopeReadResult envelope =
                CaptureAnalysisDocumentSerializer.DeserializeEnvelope(plaintext);
            if (envelope.Snapshot.DocumentRevision != header.DocumentRevision ||
                (expectedCaptureId.HasValue &&
                 envelope.Snapshot.Record.CaptureId != expectedCaptureId.Value) ||
                !string.Equals(
                    Path.GetFileName(filePath),
                    GetOpaqueFileName(envelope.Snapshot.Record.CaptureId),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Capture Analysis envelope identity is inconsistent.");
            }

            return EnvelopeLoadResult.Known(envelope);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.LogException(ex, "Quarantining an unreadable Capture Analysis envelope.");
            Quarantine(filePath);
            return EnvelopeLoadResult.Corrupt;
        }
        finally
        {
            if (plaintext != null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    private void Quarantine(string filePath)
    {
        try
        {
            string quarantineDirectory = Path.Combine(
                _localCachePathProvider.GetApplicationLocalCacheFolderPath(),
                AnalysisDirectoryName,
                MetadataVersionDirectoryName,
                QuarantineDirectoryName);
            Directory.CreateDirectory(quarantineDirectory);
            string quarantinePath = Path.Combine(
                quarantineDirectory,
                $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.corrupt");
            File.Move(filePath, quarantinePath, overwrite: false);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to quarantine a Capture Analysis envelope.");
        }
    }

    private enum EnvelopeLoadStatus
    {
        Missing,
        Known,
        ReadOnlyVersion,
        Corrupt,
    }

    private sealed record EnvelopeLoadResult(
        EnvelopeLoadStatus Status,
        CaptureAnalysisEnvelopeReadResult? Envelope)
    {
        public static EnvelopeLoadResult Missing { get; } = new(EnvelopeLoadStatus.Missing, null);

        public static EnvelopeLoadResult ReadOnly { get; } = new(EnvelopeLoadStatus.ReadOnlyVersion, null);

        public static EnvelopeLoadResult Corrupt { get; } = new(EnvelopeLoadStatus.Corrupt, null);

        public static EnvelopeLoadResult Known(CaptureAnalysisEnvelopeReadResult envelope) =>
            new(EnvelopeLoadStatus.Known, envelope);
    }
}
