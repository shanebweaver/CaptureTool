using CaptureTool.Domain;

namespace CaptureTool.Domain.Analysis;

public sealed class CanonicalCapabilityResult
{
    public CanonicalCapabilityResult(
        CaptureId captureId,
        SourceRevision sourceRevision,
        CapabilityPayload payload,
        AnalyzerIdentity analyzer,
        ProcessingBoundary processingBoundary,
        DateTimeOffset generatedAtUtc)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A capability result requires a capture ID.", nameof(captureId));
        }

        if (sourceRevision.IsEmpty)
        {
            throw new ArgumentException("A capability result requires a source revision.", nameof(sourceRevision));
        }

        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(analyzer);
        EnsureBoundary(processingBoundary);
        EnsureUtc(generatedAtUtc, nameof(generatedAtUtc));

        CaptureId = captureId;
        SourceRevision = sourceRevision;
        Payload = payload;
        Analyzer = analyzer;
        ProcessingBoundary = processingBoundary;
        GeneratedAtUtc = generatedAtUtc;
    }

    public CaptureId CaptureId { get; }

    public SourceRevision SourceRevision { get; }

    public CapabilityDefinition Capability => Payload.Definition;

    public CapabilityPayload Payload { get; }

    public AnalyzerIdentity Analyzer { get; }

    public ProcessingBoundary ProcessingBoundary { get; }

    public DateTimeOffset GeneratedAtUtc { get; }

    public bool IsEquivalentTo(CanonicalCapabilityResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CaptureId == other.CaptureId &&
            SourceRevision == other.SourceRevision &&
            Capability == other.Capability &&
            Analyzer == other.Analyzer &&
            ProcessingBoundary == other.ProcessingBoundary &&
            GeneratedAtUtc == other.GeneratedAtUtc &&
            Payload.IsEquivalentTo(other.Payload);
    }

    internal CanonicalCapabilityResult RebaseSourceRevision(SourceRevision sourceRevision)
    {
        if (!SourceRevision.HasSameBytesAs(sourceRevision))
        {
            throw new ArgumentException("A result can only be rebased to the same source bytes.", nameof(sourceRevision));
        }

        if (SourceRevision == sourceRevision)
        {
            return this;
        }

        return new(
            CaptureId,
            sourceRevision,
            Payload,
            Analyzer,
            ProcessingBoundary,
            GeneratedAtUtc);
    }

    private static void EnsureBoundary(ProcessingBoundary boundary)
    {
        if (!Enum.IsDefined(boundary) || boundary == ProcessingBoundary.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(boundary));
        }
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("An analysis timestamp must be expressed in UTC.", parameterName);
        }
    }
}

public enum CapabilityOutcomeState
{
    Unknown,
    Unsupported,
    TerminalFailure
}

public sealed class CapabilityOutcome
{
    public CapabilityOutcome(
        CaptureId captureId,
        SourceRevision sourceRevision,
        CapabilityDefinition capability,
        AnalyzerIdentity analyzer,
        ProcessingBoundary processingBoundary,
        CapabilityOutcomeState state,
        AnalysisFailure failure,
        DateTimeOffset generatedAtUtc)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A capability outcome requires a capture ID.", nameof(captureId));
        }

        if (sourceRevision.IsEmpty)
        {
            throw new ArgumentException("A capability outcome requires a source revision.", nameof(sourceRevision));
        }

        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("A capability outcome requires a capability.", nameof(capability));
        }

        ArgumentNullException.ThrowIfNull(analyzer);
        if (!Enum.IsDefined(processingBoundary) || processingBoundary == ProcessingBoundary.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(processingBoundary));
        }

        if (!Enum.IsDefined(state) || state == CapabilityOutcomeState.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (failure.Disposition != AnalysisFailureDisposition.Terminal)
        {
            throw new ArgumentException(
                "Only terminal or unsupported outcomes belong in canonical metadata.",
                nameof(failure));
        }

        if (generatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("An analysis timestamp must be expressed in UTC.", nameof(generatedAtUtc));
        }

        CaptureId = captureId;
        SourceRevision = sourceRevision;
        Capability = capability;
        Analyzer = analyzer;
        ProcessingBoundary = processingBoundary;
        State = state;
        Failure = failure;
        GeneratedAtUtc = generatedAtUtc;
    }

    public CaptureId CaptureId { get; }

    public SourceRevision SourceRevision { get; }

    public CapabilityDefinition Capability { get; }

    public AnalyzerIdentity Analyzer { get; }

    public ProcessingBoundary ProcessingBoundary { get; }

    public CapabilityOutcomeState State { get; }

    public AnalysisFailure Failure { get; }

    public DateTimeOffset GeneratedAtUtc { get; }

    public bool IsEquivalentTo(CapabilityOutcome other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CaptureId == other.CaptureId &&
            SourceRevision == other.SourceRevision &&
            Capability == other.Capability &&
            Analyzer == other.Analyzer &&
            ProcessingBoundary == other.ProcessingBoundary &&
            State == other.State &&
            Failure == other.Failure &&
            GeneratedAtUtc == other.GeneratedAtUtc;
    }

    internal CapabilityOutcome RebaseSourceRevision(SourceRevision sourceRevision)
    {
        if (!SourceRevision.HasSameBytesAs(sourceRevision))
        {
            throw new ArgumentException("An outcome can only be rebased to the same source bytes.", nameof(sourceRevision));
        }

        if (SourceRevision == sourceRevision)
        {
            return this;
        }

        return new(
            CaptureId,
            sourceRevision,
            Capability,
            Analyzer,
            ProcessingBoundary,
            State,
            Failure,
            GeneratedAtUtc);
    }
}

public sealed class CapabilityAnalysis
{
    public CapabilityAnalysis(
        CapabilityDefinition capability,
        CanonicalCapabilityResult? canonicalResult,
        CapabilityOutcome? latestOutcome)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("A capability analysis requires a capability.", nameof(capability));
        }

        if (canonicalResult == null && latestOutcome == null)
        {
            throw new ArgumentException("A capability analysis requires a result or outcome.", nameof(canonicalResult));
        }

        if (canonicalResult != null && canonicalResult.Capability != capability)
        {
            throw new ArgumentException("The canonical result does not match the capability.", nameof(canonicalResult));
        }

        if (latestOutcome != null && latestOutcome.Capability != capability)
        {
            throw new ArgumentException("The latest outcome does not match the capability.", nameof(latestOutcome));
        }

        if (canonicalResult != null && latestOutcome != null &&
            (canonicalResult.CaptureId != latestOutcome.CaptureId ||
             canonicalResult.SourceRevision != latestOutcome.SourceRevision))
        {
            throw new ArgumentException("A result and outcome must belong to the same capture source.", nameof(latestOutcome));
        }

        if (canonicalResult != null && latestOutcome != null &&
            latestOutcome.GeneratedAtUtc < canonicalResult.GeneratedAtUtc)
        {
            throw new ArgumentException(
                "The latest outcome cannot predate the canonical result.",
                nameof(latestOutcome));
        }

        Capability = capability;
        CanonicalResult = canonicalResult;
        LatestOutcome = latestOutcome;
    }

    public CapabilityDefinition Capability { get; }

    public CanonicalCapabilityResult? CanonicalResult { get; }

    public CapabilityOutcome? LatestOutcome { get; }

    public bool HasCanonicalResult => CanonicalResult != null;

    internal CapabilityAnalysis WithResult(CanonicalCapabilityResult result)
    {
        return new(Capability, result, null);
    }

    internal CapabilityAnalysis WithOutcome(CapabilityOutcome outcome)
    {
        return new(Capability, CanonicalResult, outcome);
    }

    internal CapabilityAnalysis RebaseSourceRevision(SourceRevision sourceRevision)
    {
        return new(
            Capability,
            CanonicalResult?.RebaseSourceRevision(sourceRevision),
            LatestOutcome?.RebaseSourceRevision(sourceRevision));
    }
}
