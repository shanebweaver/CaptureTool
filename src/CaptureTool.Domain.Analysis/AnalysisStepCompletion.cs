namespace CaptureTool.Domain.Analysis;

public sealed record AnalysisStepCompletion
{
    public AnalysisCapability Capability { get; }
    public AnalyzerOutcomeKind Outcome { get; }
    public string? FailureCode { get; }
    public Guid? ReusedResultId { get; }

    public AnalysisStepCompletion(AnalysisCapability capability, AnalyzerOutcomeKind outcome, string? failureCode, Guid? reusedResultId = null)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (!Enum.IsDefined(outcome) || outcome is AnalyzerOutcomeKind.Cancelled or AnalyzerOutcomeKind.InvalidSource)
            throw new ArgumentOutOfRangeException(nameof(outcome));
        if (outcome == AnalyzerOutcomeKind.Succeeded ? failureCode != null : string.IsNullOrWhiteSpace(failureCode))
            throw new ArgumentException("Success has no failure code; other outcomes require one.", nameof(failureCode));
        if (failureCode != null && (failureCode.Length > 80 || failureCode.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))))
            throw new ArgumentException("Use a bounded failure identifier.", nameof(failureCode));
        Capability = capability;
        Outcome = outcome;
        FailureCode = failureCode;
        if (reusedResultId == Guid.Empty || reusedResultId != null && outcome != AnalyzerOutcomeKind.Succeeded)
            throw new ArgumentException("Only successful steps may reuse an existing result.", nameof(reusedResultId));
        ReusedResultId = reusedResultId;
    }
}
