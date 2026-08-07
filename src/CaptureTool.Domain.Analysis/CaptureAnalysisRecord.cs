using CaptureTool.Domain;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Domain.Analysis;

public enum SourceRevisionUpdateResult
{
    Unknown,
    Unchanged,
    StampChanged,
    SourceBytesChanged
}

public sealed class CaptureAnalysisRecord
{
    public CaptureAnalysisRecord(
        CaptureId captureId,
        CaptureMediaKind mediaKind,
        DateTimeOffset capturedAtUtc,
        SourceRevision sourceRevision,
        CaptureAnalysisRecipe recipe,
        IEnumerable<CapabilityAnalysis>? analyses = null)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("An analysis record requires a capture ID.", nameof(captureId));
        }

        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A capture timestamp must be expressed in UTC.", nameof(capturedAtUtc));
        }

        if (sourceRevision.IsEmpty)
        {
            throw new ArgumentException("An analysis record requires a verified source revision.", nameof(sourceRevision));
        }

        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.MediaKind != mediaKind)
        {
            throw new ArgumentException("The analysis recipe does not support the capture media kind.", nameof(recipe));
        }

        CaptureId = captureId;
        MediaKind = mediaKind;
        CapturedAtUtc = capturedAtUtc;
        SourceRevision = sourceRevision;
        Recipe = recipe;

        foreach (CapabilityAnalysis analysis in analyses ?? [])
        {
            AddRestoredAnalysis(analysis);
        }
    }

    private readonly Dictionary<AnalysisCapabilityId, CapabilityAnalysis> _analyses = [];

    public CaptureId CaptureId { get; }

    public CaptureMediaKind MediaKind { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public SourceRevision SourceRevision { get; private set; }

    public CaptureAnalysisRecipe Recipe { get; private set; }

    public IReadOnlyCollection<CapabilityAnalysis> Analyses => _analyses.Values;

    public bool IsUsable => Recipe.Capabilities
        .Where(capability => capability.Requirement == RecipeCapabilityRequirement.Required)
        .All(capability => HasCanonicalResult(capability.Capability));

    public bool TryGetAnalysis(AnalysisCapabilityId capabilityId, out CapabilityAnalysis? analysis)
    {
        return _analyses.TryGetValue(capabilityId, out analysis);
    }

    public IReadOnlyList<RecipeCapability> GetCapabilitiesNeedingAnalysis()
    {
        return Recipe.Capabilities
            .Where(capability => !HasCanonicalResult(capability.Capability))
            .ToArray();
    }

    public SourceRevisionUpdateResult RegisterSourceRevision(SourceRevision sourceRevision)
    {
        if (sourceRevision.IsEmpty)
        {
            throw new ArgumentException("An analysis record requires a verified source revision.", nameof(sourceRevision));
        }

        if (SourceRevision == sourceRevision)
        {
            return SourceRevisionUpdateResult.Unchanged;
        }

        if (!SourceRevision.HasSameBytesAs(sourceRevision))
        {
            SourceRevision = sourceRevision;
            _analyses.Clear();
            return SourceRevisionUpdateResult.SourceBytesChanged;
        }

        SourceRevision = sourceRevision;
        foreach ((AnalysisCapabilityId id, CapabilityAnalysis analysis) in _analyses.ToArray())
        {
            _analyses[id] = analysis.RebaseSourceRevision(sourceRevision);
        }

        return SourceRevisionUpdateResult.StampChanged;
    }

    public IReadOnlyList<AnalysisCapabilityId> ApplyRecipe(CaptureAnalysisRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.Id != Recipe.Id)
        {
            throw new ArgumentException("A record cannot change to a different recipe identity.", nameof(recipe));
        }

        if (recipe.MediaKind != MediaKind)
        {
            throw new ArgumentException("The analysis recipe does not support the capture media kind.", nameof(recipe));
        }

        if (recipe.Version.Value < Recipe.Version.Value)
        {
            throw new InvalidOperationException("An analysis recipe cannot be downgraded.");
        }

        if (recipe.Version == Recipe.Version && !recipe.HasSameSemanticsAs(Recipe))
        {
            throw new InvalidOperationException("Changed recipe semantics require a new recipe version.");
        }

        var invalidated = new List<AnalysisCapabilityId>();
        foreach ((AnalysisCapabilityId id, CapabilityAnalysis analysis) in _analyses.ToArray())
        {
            if (!recipe.TryGetCapability(id, out RecipeCapability requested) ||
                requested.Capability != analysis.Capability)
            {
                _analyses.Remove(id);
                invalidated.Add(id);
            }
        }

        Recipe = recipe;
        return invalidated.AsReadOnly();
    }

    public bool InvalidateCapability(
        CapabilityDefinition currentCapability,
        AnalyzerRevision currentAnalyzerRevision)
    {
        if (currentAnalyzerRevision.IsEmpty)
        {
            throw new ArgumentException("Capability invalidation requires an analyzer revision.", nameof(currentAnalyzerRevision));
        }

        return InvalidateCapability(currentCapability, [currentAnalyzerRevision]);
    }

    public bool InvalidateCapability(
        CapabilityDefinition currentCapability,
        IEnumerable<AnalyzerRevision> currentAnalyzerRevisions)
    {
        if (currentCapability.Id.IsEmpty)
        {
            throw new ArgumentException("Capability invalidation requires a definition.", nameof(currentCapability));
        }

        ArgumentNullException.ThrowIfNull(currentAnalyzerRevisions);
        HashSet<AnalyzerRevision> revisions = [.. currentAnalyzerRevisions];
        if (revisions.Any(revision => revision.IsEmpty))
        {
            throw new ArgumentException(
                "Capability invalidation cannot use an empty analyzer revision.",
                nameof(currentAnalyzerRevisions));
        }

        if (!_analyses.TryGetValue(currentCapability.Id, out CapabilityAnalysis? analysis))
        {
            return false;
        }

        CanonicalCapabilityResult? retainedResult = analysis.Capability == currentCapability &&
            analysis.CanonicalResult is { } result &&
            revisions.Contains(result.Analyzer.Revision)
                ? analysis.CanonicalResult
                : null;
        CapabilityOutcome? retainedOutcome = analysis.Capability == currentCapability &&
            analysis.LatestOutcome is { } outcome &&
            revisions.Contains(outcome.Analyzer.Revision)
                ? analysis.LatestOutcome
                : null;

        if (retainedResult == analysis.CanonicalResult && retainedOutcome == analysis.LatestOutcome)
        {
            return false;
        }

        if (retainedResult == null && retainedOutcome == null)
        {
            _analyses.Remove(currentCapability.Id);
        }
        else
        {
            _analyses[currentCapability.Id] = new CapabilityAnalysis(
                currentCapability,
                retainedResult,
                retainedOutcome);
        }

        return true;
    }

    public bool InvalidateCapability(AnalysisCapabilityId capabilityId)
    {
        if (capabilityId.IsEmpty)
        {
            throw new ArgumentException("Capability invalidation requires an ID.", nameof(capabilityId));
        }

        return _analyses.Remove(capabilityId);
    }

    public CapabilityCommitResult TryCommitResult(
        AnalysisCommitToken token,
        AnalysisCommitPreconditions current,
        AnalyzerRevision currentAnalyzerRevision,
        CanonicalCapabilityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!CanCommit(token, current, currentAnalyzerRevision))
        {
            return CapabilityCommitResult.Stale;
        }

        ValidateResult(token, result);
        ValidatePayloadForMedia(result.Payload);

        if (_analyses.TryGetValue(token.Capability.Id, out CapabilityAnalysis? analysis) &&
            analysis.CanonicalResult is { } existing &&
            existing.IsEquivalentTo(result))
        {
            return CapabilityCommitResult.AlreadyCurrent;
        }

        if (analysis != null && result.GeneratedAtUtc < GetLatestGeneratedAtUtc(analysis))
        {
            return CapabilityCommitResult.Stale;
        }

        _analyses[token.Capability.Id] = analysis == null
            ? new CapabilityAnalysis(token.Capability, result, null)
            : analysis.WithResult(result);
        return CapabilityCommitResult.Committed;
    }

    public CapabilityCommitResult TryRecordOutcome(
        AnalysisCommitToken token,
        AnalysisCommitPreconditions current,
        AnalyzerRevision currentAnalyzerRevision,
        CapabilityOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (!CanCommit(token, current, currentAnalyzerRevision))
        {
            return CapabilityCommitResult.Stale;
        }

        ValidateOutcome(token, outcome);
        if (_analyses.TryGetValue(token.Capability.Id, out CapabilityAnalysis? analysis) &&
            analysis.LatestOutcome is { } existing &&
            existing.IsEquivalentTo(outcome))
        {
            return CapabilityCommitResult.AlreadyCurrent;
        }

        if (analysis != null && outcome.GeneratedAtUtc < GetLatestGeneratedAtUtc(analysis))
        {
            return CapabilityCommitResult.Stale;
        }

        _analyses[token.Capability.Id] = analysis == null
            ? new CapabilityAnalysis(token.Capability, null, outcome)
            : analysis.WithOutcome(outcome);
        return CapabilityCommitResult.Committed;
    }

    private bool CanCommit(
        AnalysisCommitToken token,
        AnalysisCommitPreconditions current,
        AnalyzerRevision currentAnalyzerRevision)
    {
        return Recipe.TryGetCapability(token.Capability.Id, out RecipeCapability requested) &&
            token.Matches(current, requested.Capability, currentAnalyzerRevision) &&
            current.CaptureId == CaptureId &&
            current.SourceRevision == SourceRevision &&
            current.RecipeId == Recipe.Id &&
            current.RecipeVersion == Recipe.Version;
    }

    private bool HasCanonicalResult(CapabilityDefinition capability)
    {
        return _analyses.TryGetValue(capability.Id, out CapabilityAnalysis? analysis) &&
            analysis.Capability == capability &&
            analysis.CanonicalResult != null;
    }

    private static DateTimeOffset GetLatestGeneratedAtUtc(CapabilityAnalysis analysis)
    {
        DateTimeOffset? resultTimestamp = analysis.CanonicalResult?.GeneratedAtUtc;
        DateTimeOffset? outcomeTimestamp = analysis.LatestOutcome?.GeneratedAtUtc;
        if (!resultTimestamp.HasValue)
        {
            return outcomeTimestamp!.Value;
        }

        if (!outcomeTimestamp.HasValue)
        {
            return resultTimestamp.Value;
        }

        return resultTimestamp.Value >= outcomeTimestamp.Value
            ? resultTimestamp.Value
            : outcomeTimestamp.Value;
    }

    private void AddRestoredAnalysis(CapabilityAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (!Recipe.TryGetCapability(analysis.Capability.Id, out RecipeCapability requested) ||
            requested.Capability != analysis.Capability)
        {
            throw new ArgumentException("A restored analysis is not part of the current recipe.", nameof(analysis));
        }

        CanonicalCapabilityResult? result = analysis.CanonicalResult;
        CapabilityOutcome? outcome = analysis.LatestOutcome;
        if ((result != null && (result.CaptureId != CaptureId || result.SourceRevision != SourceRevision)) ||
            (outcome != null && (outcome.CaptureId != CaptureId || outcome.SourceRevision != SourceRevision)))
        {
            throw new ArgumentException("A restored analysis belongs to another capture source.", nameof(analysis));
        }

        if (!_analyses.TryAdd(analysis.Capability.Id, analysis))
        {
            throw new ArgumentException("Only one analysis may exist per capability.", nameof(analysis));
        }

        if (result != null)
        {
            ValidatePayloadForMedia(result.Payload);
        }
    }

    private void ValidatePayloadForMedia(CapabilityPayload payload)
    {
        if (payload is MediaPropertiesV1 mediaProperties && mediaProperties.MediaKind != MediaKind)
        {
            throw new ArgumentException("Media properties do not describe this capture media kind.", nameof(payload));
        }

        if (MediaKind != CaptureMediaKind.Image && payload is OcrDocumentV1 or ImageDescriptionV1)
        {
            throw new ArgumentException("The capability payload requires an image source.", nameof(payload));
        }
    }

    private static void ValidateResult(AnalysisCommitToken token, CanonicalCapabilityResult result)
    {
        if (result.CaptureId != token.CaptureId ||
            result.SourceRevision != token.SourceRevision ||
            result.Capability != token.Capability ||
            result.Analyzer.Revision != token.AnalyzerRevision)
        {
            throw new ArgumentException("The result does not match its commit token.", nameof(result));
        }
    }

    private static void ValidateOutcome(AnalysisCommitToken token, CapabilityOutcome outcome)
    {
        if (outcome.CaptureId != token.CaptureId ||
            outcome.SourceRevision != token.SourceRevision ||
            outcome.Capability != token.Capability ||
            outcome.Analyzer.Revision != token.AnalyzerRevision)
        {
            throw new ArgumentException("The outcome does not match its commit token.", nameof(outcome));
        }
    }
}
