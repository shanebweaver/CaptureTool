using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Analyzers;

public sealed record CaptureAnalyzerResolutionRequest
{
    private readonly IReadOnlyList<AnalyzerRevision> _attemptedAnalyzers;

    public CaptureAnalyzerResolutionRequest(
        CapabilityDefinition capability,
        CaptureMediaKind mediaKind,
        long sourceLength,
        AnalysisPurpose purpose,
        AnalysisProcessingPolicy processingPolicy,
        long resolutionPolicyRevision,
        IEnumerable<AnalyzerRevision>? attemptedAnalyzers = null)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("Analyzer resolution requires a capability.", nameof(capability));
        }

        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        if (sourceLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceLength));
        }

        if (purpose.IsEmpty)
        {
            throw new ArgumentException("Analyzer resolution requires a purpose.", nameof(purpose));
        }

        ArgumentNullException.ThrowIfNull(processingPolicy);
        if (processingPolicy.AuthorizedPurpose != purpose)
        {
            throw new ArgumentException("The processing policy does not authorize the requested purpose.", nameof(processingPolicy));
        }

        if (resolutionPolicyRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resolutionPolicyRevision));
        }

        AnalyzerRevision[] attempted = [.. attemptedAnalyzers ?? []];
        if (attempted.Any(revision => revision.IsEmpty))
        {
            throw new ArgumentException("Attempted analyzer revisions cannot be empty.", nameof(attemptedAnalyzers));
        }

        Capability = capability;
        MediaKind = mediaKind;
        SourceLength = sourceLength;
        Purpose = purpose;
        ProcessingPolicy = processingPolicy;
        ResolutionPolicyRevision = resolutionPolicyRevision;
        _attemptedAnalyzers = Array.AsReadOnly(attempted);
    }

    public CapabilityDefinition Capability { get; }

    public CaptureMediaKind MediaKind { get; }

    public long SourceLength { get; }

    public AnalysisPurpose Purpose { get; }

    public AnalysisProcessingPolicy ProcessingPolicy { get; }

    public long ResolutionPolicyRevision { get; }

    public IReadOnlyList<AnalyzerRevision> AttemptedAnalyzers => _attemptedAnalyzers;
}

public enum CaptureAnalyzerEligibilityStatus
{
    Unknown,
    Eligible,
    AnalysisFeatureDisabled,
    AnalyzerFeatureDisabled,
    UnsupportedCapability,
    UnsupportedMediaKind,
    BoundaryNotAuthorized,
    ProviderNotAuthorized,
    PurposeNotAuthorized,
    PreparationRequired,
    Unavailable,
}

public sealed record CaptureAnalyzerCandidateEvaluation
{
    public CaptureAnalyzerCandidateEvaluation(
        CaptureAnalyzerDescriptor descriptor,
        CaptureAnalyzerEligibilityStatus eligibility,
        CaptureAnalyzerAvailability? availability = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!Enum.IsDefined(eligibility) || eligibility == CaptureAnalyzerEligibilityStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(eligibility));
        }

        bool mustNotProbe = eligibility is CaptureAnalyzerEligibilityStatus.AnalysisFeatureDisabled or
            CaptureAnalyzerEligibilityStatus.AnalyzerFeatureDisabled or
            CaptureAnalyzerEligibilityStatus.BoundaryNotAuthorized or
            CaptureAnalyzerEligibilityStatus.ProviderNotAuthorized or
            CaptureAnalyzerEligibilityStatus.PurposeNotAuthorized;
        if (mustNotProbe && availability != null)
        {
            throw new ArgumentException("Policy-ineligible analyzers must be filtered before availability probing.");
        }

        Descriptor = descriptor;
        Eligibility = eligibility;
        Availability = availability;
    }

    public CaptureAnalyzerDescriptor Descriptor { get; }

    public CaptureAnalyzerEligibilityStatus Eligibility { get; }

    public CaptureAnalyzerAvailability? Availability { get; }
}

public enum CaptureAnalyzerResolutionStatus
{
    Unknown,
    Resolved,
    FeatureDisabled,
    WaitingForPreparation,
    NoEligibleAnalyzer,
}

public sealed record CaptureAnalyzerResolution
{
    private readonly IReadOnlyList<CaptureAnalyzerCandidateEvaluation> _candidates;

    private CaptureAnalyzerResolution(
        CaptureAnalyzerResolutionStatus status,
        ICaptureAnalyzer? analyzer,
        IEnumerable<CaptureAnalyzerCandidateEvaluation> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        CaptureAnalyzerCandidateEvaluation[] copiedCandidates = [.. candidates];
        if (copiedCandidates.Any(candidate => candidate == null))
        {
            throw new ArgumentException("Analyzer candidates cannot contain null values.", nameof(candidates));
        }

        Status = status;
        Analyzer = analyzer;
        _candidates = Array.AsReadOnly(copiedCandidates);
    }

    public CaptureAnalyzerResolutionStatus Status { get; }

    public ICaptureAnalyzer? Analyzer { get; }

    public IReadOnlyList<CaptureAnalyzerCandidateEvaluation> Candidates => _candidates;

    public static CaptureAnalyzerResolution Resolved(
        ICaptureAnalyzer analyzer,
        IEnumerable<CaptureAnalyzerCandidateEvaluation> candidates)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        var resolution = new CaptureAnalyzerResolution(
            CaptureAnalyzerResolutionStatus.Resolved,
            analyzer,
            candidates);
        if (!resolution.Candidates.Any(candidate =>
            candidate.Descriptor == analyzer.Descriptor &&
            candidate.Eligibility == CaptureAnalyzerEligibilityStatus.Eligible &&
            candidate.Availability?.Status == CaptureAnalyzerAvailabilityStatus.Available))
        {
            throw new ArgumentException(
                "A resolved analyzer requires a matching eligible and available candidate.",
                nameof(candidates));
        }

        return resolution;
    }

    public static CaptureAnalyzerResolution FeatureDisabled(
        IEnumerable<CaptureAnalyzerCandidateEvaluation> candidates)
    {
        return CreateUnresolved(
            CaptureAnalyzerResolutionStatus.FeatureDisabled,
            candidates,
            null);
    }

    public static CaptureAnalyzerResolution WaitingForPreparation(
        IEnumerable<CaptureAnalyzerCandidateEvaluation> candidates)
    {
        return CreateUnresolved(
            CaptureAnalyzerResolutionStatus.WaitingForPreparation,
            candidates,
            CaptureAnalyzerEligibilityStatus.PreparationRequired);
    }

    public static CaptureAnalyzerResolution NoEligibleAnalyzer(
        IEnumerable<CaptureAnalyzerCandidateEvaluation> candidates)
    {
        var resolution = new CaptureAnalyzerResolution(
            CaptureAnalyzerResolutionStatus.NoEligibleAnalyzer,
            null,
            candidates);
        if (resolution.Candidates.Any(candidate =>
            candidate.Eligibility == CaptureAnalyzerEligibilityStatus.Eligible))
        {
            throw new ArgumentException(
                "A no-eligible-analyzer resolution cannot contain an eligible candidate.",
                nameof(candidates));
        }

        return resolution;
    }

    private static CaptureAnalyzerResolution CreateUnresolved(
        CaptureAnalyzerResolutionStatus status,
        IEnumerable<CaptureAnalyzerCandidateEvaluation> candidates,
        CaptureAnalyzerEligibilityStatus? requiredEligibility)
    {
        var resolution = new CaptureAnalyzerResolution(status, null, candidates);
        if ((requiredEligibility.HasValue &&
             !resolution.Candidates.Any(candidate => candidate.Eligibility == requiredEligibility.Value)) ||
            resolution.Candidates.Any(candidate => candidate.Eligibility == CaptureAnalyzerEligibilityStatus.Eligible))
        {
            throw new ArgumentException(
                "The candidate evaluations do not match the unresolved resolution status.",
                nameof(candidates));
        }

        return resolution;
    }
}

public interface ICaptureAnalyzerResolver
{
    ValueTask<CaptureAnalyzerResolution> ResolveAsync(
        CaptureAnalyzerResolutionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICaptureAnalyzerCatalog
{
    IReadOnlyList<ICaptureAnalyzer> Analyzers { get; }

    ICaptureAnalyzer? Find(
        AnalyzerRevision revision,
        CapabilityDefinition capability);
}
