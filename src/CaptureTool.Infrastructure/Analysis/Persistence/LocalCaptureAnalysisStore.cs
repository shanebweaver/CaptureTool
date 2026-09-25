using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Persistence.Serialization;
using CaptureTool.Infrastructure.Persistence;

namespace CaptureTool.Infrastructure.Analysis.Persistence;

/// <summary>
/// Single-process store. A short gate serializes publication and generation changes;
/// model execution never holds it. Old generations cannot be read or written after clear.
/// </summary>
internal sealed partial class LocalCaptureAnalysisStore : ICaptureAnalysisStore, IAnalysisExecutionStore, IDisposable
{
    private readonly string _root;
    private readonly string _controlPath;
    private readonly string _recordsRoot;
    private readonly IProtectedFileSystem _files;
    private readonly ProtectedDocumentFile _documents;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalCaptureAnalysisStore(IStorageService storage, IUserDataProtector protector)
        : this(storage, protector, new LocalProtectedFileSystem()) { }

    internal LocalCaptureAnalysisStore(IStorageService storage, IUserDataProtector protector, IProtectedFileSystem files)
    {
        _root = Path.Combine(storage.GetApplicationDataFolderPath(), "CaptureAnalysis");
        _controlPath = Path.Combine(_root, "control.bin");
        _recordsRoot = Path.Combine(_root, "records");
        _files = files;
        _documents = new(protector, files);
    }

    public async Task<AnalysisCleanupResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument control = (await ReadControlAsync(true, cancellationToken).ConfigureAwait(false))!;
            DeleteControlTemporaries();
            return CleanupObsoleteFiles(control.Generation);
        }
        finally { _gate.Release(); }
    }

    public async Task<AnalysisWriteToken> BeginRunAsync(CaptureId captureId, AnalysisMediaKind mediaKind,
        SourceRevision sourceRevision, string planVersion, CancellationToken cancellationToken = default)
    {
        // Validate before persisting any control state.
        var next = new CaptureAnalysisRecord(captureId, mediaKind, sourceRevision, planVersion, Guid.NewGuid(), []);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument control = (await ReadControlAsync(true, cancellationToken).ConfigureAwait(false))!;
            if ((await ReadDocumentAsync(control.Generation, captureId, cancellationToken).ConfigureAwait(false))?.Run != null)
                throw new InvalidOperationException("Execution requests must be admitted through the execution store.");
            CaptureAnalysisRecord? previous = await ReadRecordAsync(control.Generation, captureId, cancellationToken).ConfigureAwait(false);
            if (previous != null)
            {
                if (previous.MediaKind != mediaKind) throw new InvalidOperationException("A capture cannot change media kind.");
                next = previous.StartRun(sourceRevision, planVersion, next.RunId);
            }

            await WriteRecordAsync(control.Generation, next, cancellationToken).ConfigureAwait(false);
            return new(captureId, sourceRevision, control.Generation, next.RunId);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> TryWriteAsync(AnalysisWriteToken token, AnalysisResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(result);
        ValidateId(token.CaptureId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            if (control == null || control.Generation != token.Generation) return false;
            if ((await ReadDocumentAsync(control.Generation, token.CaptureId, cancellationToken).ConfigureAwait(false))?.Run != null) return false;
            CaptureAnalysisRecord? current = await ReadRecordAsync(control.Generation, token.CaptureId, cancellationToken).ConfigureAwait(false);
            if (current == null || current.RunId != token.RunId || current.SourceRevision != token.SourceRevision) return false;
            await WriteRecordAsync(control.Generation, current.WithResult(result), cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<CaptureAnalysisRecord?> GetAsync(CaptureId captureId, SourceRevision? expectedSource = null,
        CancellationToken cancellationToken = default)
    {
        ValidateId(captureId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            if (control == null) return null;
            CaptureAnalysisRecord? record = await ReadRecordAsync(control.Generation, captureId, cancellationToken).ConfigureAwait(false);
            return expectedSource != null && record?.SourceRevision != expectedSource ? null : record;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<CaptureAnalysisRecord>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            if (control == null) return [];
            List<CaptureAnalysisRecord> records = [];
            foreach (string path in _files.GetFiles(GenerationPath(control.Generation)).Order(StringComparer.Ordinal))
            {
                if (!path.EndsWith(".analysis", StringComparison.Ordinal)) continue;
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out Guid id) || id == Guid.Empty)
                    throw new InvalidDataException("Unexpected metadata filename.");
                CaptureAnalysisRecord? record = await ReadRecordAsync(control.Generation, new CaptureId(id), cancellationToken).ConfigureAwait(false);
                if (record != null) records.Add(record);
            }

            return records.AsReadOnly();
        }
        finally { _gate.Release(); }
    }

    public Task<AnalysisCleanupResult> ClearAsync(CancellationToken cancellationToken = default) =>
        ClearAsync(long.MaxValue, cancellationToken); // Without a catalog watermark, automatic historical reconciliation must fail closed.

    public async Task<AnalysisCleanupResult> ClearAsync(long reconciliationBoundary, CancellationToken cancellationToken = default)
    {
        if (reconciliationBoundary < 0) throw new ArgumentOutOfRangeException(nameof(reconciliationBoundary));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Explicit deletion must also recover unreadable or missing control. Ordinary access still fails closed.
            // Publish a protected generation before touching existing analysis files, even during recovery.
            var next = new AnalysisControlDocument(2, Guid.NewGuid(), ReconciliationBoundary: reconciliationBoundary);
            await _documents.WriteAsync(_controlPath, next, AnalysisJsonContext.Default.AnalysisControlDocument, cancellationToken).ConfigureAwait(false);
            // After publication, deletion is committed even if cleanup is interrupted. Do not roll back the generation.
            return CleanupObsoleteFiles(next.Generation);
        }
        finally { _gate.Release(); }
    }

    private async Task<AnalysisControlDocument?> ReadControlAsync(bool create, CancellationToken cancellationToken)
    {
        AnalysisControlDocument? control = await _documents.ReadAsync(_controlPath,
            AnalysisJsonContext.Default.AnalysisControlDocument, cancellationToken).ConfigureAwait(false);
        if (control == null)
        {
            // A crash before the very first atomic publication can leave only a control temporary.
            // Any other file or directory is evidence of an existing store: never silently reset it.
            if (_files.GetFiles(_root).Any(path => !IsControlTemporary(path)) || _files.GetDirectories(_root).Length != 0)
                throw new InvalidDataException("Analysis control is missing from an existing store.");
            if (!create) return null;
            DeleteControlTemporaries();
            control = new(1, Guid.NewGuid());
            await _documents.WriteAsync(_controlPath, control, AnalysisJsonContext.Default.AnalysisControlDocument, cancellationToken).ConfigureAwait(false);
        }

        if (control.Version is not (1 or 2) || control.Generation == Guid.Empty || control.QueueOrder < 0 || control.ReconciliationBoundary < 0)
            throw new InvalidDataException("Unsupported or invalid analysis control.");
        return control;
    }

    private async Task<CaptureAnalysisRecord?> ReadRecordAsync(Guid generation, CaptureId id, CancellationToken cancellationToken)
    {
        AnalysisDocument? document = await ReadDocumentAsync(generation, id, cancellationToken).ConfigureAwait(false);
        return document?.SourceSha256 == null ? null : AnalysisDocumentMapper.ToRecord(document);
    }

    private Task WriteRecordAsync(Guid generation, CaptureAnalysisRecord record, CancellationToken cancellationToken) =>
        _documents.WriteAsync(RecordPath(generation, record.CaptureId), AnalysisDocumentMapper.ToDocument(record),
            AnalysisJsonContext.Default.AnalysisDocument, cancellationToken);

    private AnalysisCleanupResult CleanupObsoleteFiles(Guid current)
    {
        int remaining = 0;
        bool currentPending = false;
        // Initialization holds the publication gate, so no current temporary can
        // belong to an active write in this single-process store.
        foreach (string path in _files.GetFiles(GenerationPath(current)).Where(IsAnalysisTemporary))
        {
            try { _files.DeleteFile(path); }
            catch (IOException) { currentPending = true; }
            catch (UnauthorizedAccessException) { currentPending = true; }
        }
        if (currentPending) remaining++;
        foreach (string directory in _files.GetDirectories(_recordsRoot))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out Guid generation))
                throw new InvalidDataException("Unexpected analysis generation directory.");
            if (generation == current) continue;
            try { _files.DeleteDocumentDirectory(directory); }
            catch (IOException) { remaining++; }
            catch (UnauthorizedAccessException) { remaining++; }
        }

        return new(remaining == 0, remaining);
    }

    private string GenerationPath(Guid generation) => Path.Combine(_recordsRoot, generation.ToString("N"));
    private string RecordPath(Guid generation, CaptureId captureId) => Path.Combine(GenerationPath(generation), captureId + ".analysis");

    private static void ValidateId(CaptureId captureId)
    {
        if (captureId.IsEmpty) throw new ArgumentException("Capture identity is required.", nameof(captureId));
    }

    public void Dispose() => _gate.Dispose();

    private void DeleteControlTemporaries()
    {
        foreach (string path in _files.GetFiles(_root).Where(IsControlTemporary)) _files.DeleteFile(path);
    }

    private static bool IsControlTemporary(string path)
    {
        const string prefix = "control.bin.";
        string name = Path.GetFileName(path);
        return name.Length == prefix.Length + 32 + 4 && name.StartsWith(prefix, StringComparison.Ordinal) &&
            name.EndsWith(".tmp", StringComparison.Ordinal) && Guid.TryParseExact(name[prefix.Length..^4], "N", out _);
    }

    private static bool IsAnalysisTemporary(string path)
    {
        string[] parts = Path.GetFileName(path).Split('.');
        return parts.Length == 4 && parts[1] == "analysis" && parts[3] == "tmp" &&
            Guid.TryParseExact(parts[0], "N", out Guid capture) && capture != Guid.Empty &&
            Guid.TryParseExact(parts[2], "N", out _);
    }
}
