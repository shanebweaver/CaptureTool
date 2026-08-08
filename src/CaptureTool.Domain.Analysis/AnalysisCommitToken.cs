using CaptureTool.Domain;

namespace CaptureTool.Domain.Analysis;

public readonly record struct AnalysisCommitPreconditions
{
    public AnalysisCommitPreconditions(
        CaptureId captureId,
        long captureSourceGeneration,
        ProvisionalSourceStamp sourceStamp,
        SourceRevision sourceRevision,
        AnalysisPurpose purpose,
        long policyRevision,
        long controlGeneration,
        long enrollmentGeneration,
        long tombstoneGeneration,
        AnalysisRecipeId recipeId,
        AnalysisRecipeVersion recipeVersion,
        long resolutionPolicyRevision)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Commit preconditions require a capture ID.", nameof(captureId));
        }

        EnsurePositive(captureSourceGeneration, nameof(captureSourceGeneration));
        if (!sourceStamp.IsKnown)
        {
            throw new ArgumentException("Commit preconditions require a known source stamp.", nameof(sourceStamp));
        }

        if (sourceRevision.IsEmpty)
        {
            throw new ArgumentException("Commit preconditions require a verified source revision.", nameof(sourceRevision));
        }

        if (!sourceRevision.Matches(sourceStamp))
        {
            throw new ArgumentException(
                "The verified source revision must match the expected source stamp.",
                nameof(sourceRevision));
        }

        if (purpose.IsEmpty)
        {
            throw new ArgumentException("Commit preconditions require a purpose.", nameof(purpose));
        }

        EnsurePositive(policyRevision, nameof(policyRevision));
        EnsureNonNegative(controlGeneration, nameof(controlGeneration));
        EnsurePositive(enrollmentGeneration, nameof(enrollmentGeneration));
        EnsureNonNegative(tombstoneGeneration, nameof(tombstoneGeneration));
        if (recipeId.IsEmpty)
        {
            throw new ArgumentException("Commit preconditions require a recipe ID.", nameof(recipeId));
        }

        if (recipeVersion.IsEmpty)
        {
            throw new ArgumentException("Commit preconditions require a recipe version.", nameof(recipeVersion));
        }

        EnsurePositive(resolutionPolicyRevision, nameof(resolutionPolicyRevision));

        CaptureId = captureId;
        CaptureSourceGeneration = captureSourceGeneration;
        SourceStamp = sourceStamp;
        SourceRevision = sourceRevision;
        Purpose = purpose;
        PolicyRevision = policyRevision;
        ControlGeneration = controlGeneration;
        EnrollmentGeneration = enrollmentGeneration;
        TombstoneGeneration = tombstoneGeneration;
        RecipeId = recipeId;
        RecipeVersion = recipeVersion;
        ResolutionPolicyRevision = resolutionPolicyRevision;
    }

    public CaptureId CaptureId { get; }

    // Advances when the retained analysis source or active/deleted state changes, not when only a
    // preferred open/export location changes.
    public long CaptureSourceGeneration { get; }

    public ProvisionalSourceStamp SourceStamp { get; }

    public SourceRevision SourceRevision { get; }

    public AnalysisPurpose Purpose { get; }

    public long PolicyRevision { get; }

    public long ControlGeneration { get; }

    public long EnrollmentGeneration { get; }

    public long TombstoneGeneration { get; }

    public AnalysisRecipeId RecipeId { get; }

    public AnalysisRecipeVersion RecipeVersion { get; }

    public long ResolutionPolicyRevision { get; }

    private static void EnsurePositive(long value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A revision must be positive.");
        }
    }

    private static void EnsureNonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A generation cannot be negative.");
        }
    }
}

public readonly record struct AnalysisCommitToken
{
    public AnalysisCommitToken(
        AnalysisCommitPreconditions expected,
        CapabilityDefinition capability,
        AnalyzerRevision analyzerRevision)
    {
        if (expected.CaptureId.IsEmpty)
        {
            throw new ArgumentException("A commit token requires expected preconditions.", nameof(expected));
        }

        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("A commit token requires a capability.", nameof(capability));
        }

        if (analyzerRevision.IsEmpty)
        {
            throw new ArgumentException("A commit token requires an analyzer revision.", nameof(analyzerRevision));
        }

        Expected = expected;
        Capability = capability;
        AnalyzerRevision = analyzerRevision;
    }

    public AnalysisCommitPreconditions Expected { get; }

    public CapabilityDefinition Capability { get; }

    public AnalyzerRevision AnalyzerRevision { get; }

    public CaptureId CaptureId => Expected.CaptureId;

    public SourceRevision SourceRevision => Expected.SourceRevision;

    public bool Matches(
        AnalysisCommitPreconditions current,
        CapabilityDefinition currentCapability,
        AnalyzerRevision currentAnalyzerRevision)
    {
        return Expected == current &&
            Capability == currentCapability &&
            AnalyzerRevision == currentAnalyzerRevision;
    }
}

public enum CapabilityCommitResult
{
    Unknown,
    Committed,
    AlreadyCurrent,
    Stale
}
