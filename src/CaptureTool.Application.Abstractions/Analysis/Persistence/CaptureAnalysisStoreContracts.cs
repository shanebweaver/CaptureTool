using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Persistence;

public sealed record CaptureAnalysisStoreSnapshot
{
    public CaptureAnalysisStoreSnapshot(long documentRevision, CaptureAnalysisRecord record)
    {
        if (documentRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentRevision));
        }

        ArgumentNullException.ThrowIfNull(record);
        DocumentRevision = documentRevision;
        Record = record;
    }

    public long DocumentRevision { get; }

    public CaptureAnalysisRecord Record { get; }
}

public enum CaptureAnalysisStoreWriteStatus
{
    Unknown,
    Succeeded,
    NotFound,
    Conflict,
    StaleCommit,
    ReadOnlyVersion,
    Unavailable,
}

public sealed record CaptureAnalysisStoreWriteResult
{
    public CaptureAnalysisStoreWriteResult(
        CaptureAnalysisStoreWriteStatus status,
        CaptureAnalysisStoreSnapshot? snapshot = null)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisStoreWriteStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (status == CaptureAnalysisStoreWriteStatus.Succeeded && snapshot == null)
        {
            throw new ArgumentException("A successful metadata write requires a snapshot.", nameof(snapshot));
        }

        Status = status;
        Snapshot = snapshot;
    }

    public CaptureAnalysisStoreWriteStatus Status { get; }

    public CaptureAnalysisStoreSnapshot? Snapshot { get; }
}

public sealed record CaptureAnalysisSourceRegistration
{
    private readonly IReadOnlyList<RecipeCapability> _capabilities;

    public CaptureAnalysisSourceRegistration(
        AnalysisCommitPreconditions preconditions,
        CaptureMediaKind mediaKind,
        DateTimeOffset capturedAtUtc,
        CaptureAnalysisRecipe recipe,
        IEnumerable<AnalysisCapabilityId>? capabilityIds = null)
    {
        if (preconditions.CaptureId.IsEmpty)
        {
            throw new ArgumentException("Source registration requires commit preconditions.", nameof(preconditions));
        }

        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A captured timestamp must be expressed in UTC.", nameof(capturedAtUtc));
        }

        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.Id != preconditions.RecipeId || recipe.Version != preconditions.RecipeVersion ||
            recipe.MediaKind != mediaKind)
        {
            throw new ArgumentException("The recipe must match the source registration preconditions.", nameof(recipe));
        }

        Preconditions = preconditions;
        MediaKind = mediaKind;
        CapturedAtUtc = capturedAtUtc;
        Recipe = recipe;

        AnalysisCapabilityId[] selectedCapabilityIds = [.. capabilityIds ?? []];
        if (selectedCapabilityIds.Any(capabilityId => capabilityId.IsEmpty) ||
            selectedCapabilityIds.Distinct().Count() != selectedCapabilityIds.Length)
        {
            throw new ArgumentException(
                "Selected capabilities must contain distinct, non-empty IDs.",
                nameof(capabilityIds));
        }

        RecipeCapability[] selectedCapabilities = selectedCapabilityIds.Length == 0
            ? [.. recipe.Capabilities]
            : recipe.Capabilities
                .Where(capability => selectedCapabilityIds.Contains(capability.Capability.Id))
                .ToArray();
        if (selectedCapabilities.Length == 0 ||
            selectedCapabilityIds.Length > 0 &&
            selectedCapabilities.Length != selectedCapabilityIds.Length)
        {
            throw new ArgumentException(
                "Every selected capability must belong to the analysis recipe.",
                nameof(capabilityIds));
        }

        _capabilities = Array.AsReadOnly(selectedCapabilities);
    }

    public AnalysisCommitPreconditions Preconditions { get; }

    public CaptureMediaKind MediaKind { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public CaptureAnalysisRecipe Recipe { get; }

    public IReadOnlyList<RecipeCapability> Capabilities => _capabilities;
}

public readonly record struct CaptureAnalysisDeletionToken
{
    public CaptureAnalysisDeletionToken(
        CaptureId captureId,
        long controlGeneration,
        long tombstoneGeneration)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Deletion requires a capture ID.", nameof(captureId));
        }

        if (controlGeneration < 0 || tombstoneGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tombstoneGeneration));
        }

        CaptureId = captureId;
        ControlGeneration = controlGeneration;
        TombstoneGeneration = tombstoneGeneration;
    }

    public CaptureId CaptureId { get; }

    public long ControlGeneration { get; }

    public long TombstoneGeneration { get; }
}

public interface ICaptureAnalysisStore
{
    ValueTask<CaptureAnalysisStoreSnapshot?> GetAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<CaptureAnalysisStoreSnapshot> ReadAllAsync(
        CancellationToken cancellationToken = default);
}

// Implementations own the process-wide per-capture mutation gate. Each operation re-reads the
// current Capture Asset, control enrollment/tombstone, policy, and metadata revision while holding
// that gate before conditionally mutating metadata.
public interface ICaptureAnalysisMutationCoordinator
{
    ValueTask<CaptureAnalysisStoreWriteResult> TryRegisterSourceAsync(
        CaptureAnalysisSourceRegistration registration,
        long? expectedDocumentRevision,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisStoreWriteResult> TryCommitCapabilityAsync(
        AnalysisCommitToken commitToken,
        CanonicalCapabilityResult result,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisStoreWriteResult> TryCommitCapabilityAsync(
        AnalysisCommitToken commitToken,
        CapabilityOutcome outcome,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisStoreWriteResult> TryDeleteAsync(
        CaptureAnalysisDeletionToken deletionToken,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default);
}
