using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Infrastructure.Analysis.Persistence;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Memory;

internal sealed class LocalCaptureMemoryOperationStore(
    IStorageService storage,
    IUserDataProtectionService protection,
    IAtomicFileWriter writer) : ICaptureMemoryOperationStore, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CaptureMemoryOperationSnapshot? _snapshot;
    internal string FilePath => Path.Combine(storage.GetApplicationDataFolderPath(),
        "CaptureAnalysis", "operations-v1", "current.operation");

    public async ValueTask<CaptureMemoryOperationSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return Load(); }
        finally { _gate.Release(); }
    }

    public async ValueTask<bool> TryWriteAsync(CaptureMemoryOperation operation, long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CaptureMemoryOperationSnapshot current = Load();
            if (current.Revision != expectedRevision)
            {
                return false;
            }
            long revision = checked(current.Revision + 1);
            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(
                OperationDocument.From(operation, revision), OperationJsonContext.Default.OperationDocument);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.Write(FilePath, protection.Protect(plaintext));
                _snapshot = new(revision, operation);
                return true;
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        finally { _gate.Release(); }
    }

    private CaptureMemoryOperationSnapshot Load()
    {
        if (_snapshot != null) { return _snapshot; }
        if (!File.Exists(FilePath)) { return _snapshot = new(0, null); }
        byte[] plaintext = protection.Unprotect(File.ReadAllBytes(FilePath));
        try
        {
            OperationDocument document = JsonSerializer.Deserialize(plaintext,
                OperationJsonContext.Default.OperationDocument) ?? throw new InvalidDataException("Empty operation journal.");
            if (document.SchemaVersion != 1 || document.Revision <= 0)
            {
                throw new InvalidDataException("Unsupported Capture Memory operation journal.");
            }
            return _snapshot = new(document.Revision, document.ToOperation());
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public void Dispose() => _gate.Dispose();
}

internal sealed class OperationDocument
{
    public int SchemaVersion { get; set; } = 1;
    public long Revision { get; set; }
    public Guid Id { get; set; }
    public CaptureMemoryOperationKind Kind { get; set; }
    public bool IncludeExistingCaptures { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public long ControlGeneration { get; set; }
    public long PolicyRevision { get; set; }
    public CaptureMemoryOperationPhase Phase { get; set; }
    public CaptureMemoryOperationStatus Status { get; set; }
    public string[] CaptureIds { get; set; } = [];
    public int AffectedCaptureCount { get; set; }
    public bool HasLimitedModelCoverage { get; set; }
    public bool IsSchedulingComplete { get; set; }

    public CaptureMemoryOperation ToOperation() => new(Id, new(Kind, IncludeExistingCaptures), StartedAtUtc,
        ControlGeneration, PolicyRevision, Phase, Status, CaptureIds.Select(CaptureId.Parse),
        AffectedCaptureCount, HasLimitedModelCoverage, IsSchedulingComplete);

    public static OperationDocument From(CaptureMemoryOperation value, long revision) => new()
    {
        Revision = revision, Id = value.Id, Kind = value.Request.Kind,
        IncludeExistingCaptures = value.Request.IncludeExistingCaptures, StartedAtUtc = value.StartedAtUtc,
        ControlGeneration = value.ControlGeneration, PolicyRevision = value.PolicyRevision, Phase = value.Phase,
        Status = value.Status, CaptureIds = value.CaptureIds.Select(id => id.ToString()).ToArray(),
        AffectedCaptureCount = value.AffectedCaptureCount, HasLimitedModelCoverage = value.HasLimitedModelCoverage,
        IsSchedulingComplete = value.IsSchedulingComplete,
    };
}

[JsonSerializable(typeof(OperationDocument))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OperationJsonContext : JsonSerializerContext;
