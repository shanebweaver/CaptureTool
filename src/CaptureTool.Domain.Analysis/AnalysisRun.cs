namespace CaptureTool.Domain.Analysis;

public enum AnalysisRunStatus { Queued, Running, Completed, Cancelled, InvalidSource, Failed }
public enum AnalyzerOutcomeKind { Succeeded, Unsupported, TemporarilyUnavailable, Failed, InvalidSource, ContentRejected, Cancelled }

public sealed record AnalysisStepCompletion
{
    public AnalysisCapability Capability { get; }
    public AnalyzerOutcomeKind Outcome { get; }
    public string? FailureCode { get; }

    public AnalysisStepCompletion(AnalysisCapability capability, AnalyzerOutcomeKind outcome, string? failureCode)
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
    }
}

/// <summary>Durable ordered execution state; successful metadata has an independent lifetime.</summary>
public sealed class AnalysisRun
{
    public Guid Id { get; }
    public Guid AuthorizationId { get; }
    public long QueueOrder { get; }
    public string PlanVersion { get; }
    public SourceRevision? SourceRevision { get; }
    public AnalysisRunStatus Status { get; }
    public IReadOnlyList<AnalysisCapability> Steps { get; }
    public IReadOnlyList<AnalysisStepCompletion> CompletedSteps { get; }
    public bool IsPending => Status is AnalysisRunStatus.Queued or AnalysisRunStatus.Running;

    public AnalysisRun(Guid id, Guid authorizationId, long queueOrder, string planVersion,
        SourceRevision? sourceRevision, AnalysisRunStatus status, IEnumerable<AnalysisCapability> steps,
        IEnumerable<AnalysisStepCompletion> completedSteps)
    {
        if (id == Guid.Empty || authorizationId == Guid.Empty) throw new ArgumentException("Run and authorization identities are required.");
        if (queueOrder <= 0) throw new ArgumentOutOfRangeException(nameof(queueOrder));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Id = id;
        AuthorizationId = authorizationId;
        QueueOrder = queueOrder;
        PlanVersion = AnalysisGuard.Identifier(planVersion, nameof(planVersion));
        SourceRevision = sourceRevision;
        Status = status;
        Steps = AnalysisGuard.Freeze(steps);
        CompletedSteps = AnalysisGuard.Freeze(completedSteps);
        if (Steps.Count == 0 || Steps.Distinct().Count() != Steps.Count || CompletedSteps.Count > Steps.Count ||
            !CompletedSteps.Select(step => step.Capability).SequenceEqual(Steps.Take(CompletedSteps.Count)))
            throw new ArgumentException("Completed steps must be an ordered prefix of a distinct plan.");
        if ((status is AnalysisRunStatus.Running or AnalysisRunStatus.Completed || CompletedSteps.Count > 0) && sourceRevision == null)
            throw new ArgumentException("Executed work requires a verified source revision.");
        if (status == AnalysisRunStatus.Queued && (sourceRevision != null || CompletedSteps.Count != 0) ||
            status == AnalysisRunStatus.Completed && CompletedSteps.Count != Steps.Count ||
            status == AnalysisRunStatus.Running && CompletedSteps.Count == Steps.Count)
            throw new ArgumentException("Run state and completion disagree.");
    }

    public AnalysisRun BindSource(SourceRevision revision)
    {
        if (!IsPending || SourceRevision != null && SourceRevision != revision)
            throw new InvalidOperationException("Cannot resume this run with a different source.");
        return new(Id, AuthorizationId, QueueOrder, PlanVersion, revision, AnalysisRunStatus.Running, Steps, CompletedSteps);
    }

    public AnalysisRun CompleteStep(AnalysisStepCompletion step)
    {
        if (Status != AnalysisRunStatus.Running || step.Capability != Steps[CompletedSteps.Count])
            throw new InvalidOperationException("Only the next running step can complete.");
        return new(Id, AuthorizationId, QueueOrder, PlanVersion, SourceRevision,
            CompletedSteps.Count + 1 == Steps.Count ? AnalysisRunStatus.Completed : AnalysisRunStatus.Running,
            Steps, CompletedSteps.Append(step));
    }

    public AnalysisRun Finish(AnalysisRunStatus status)
    {
        if (status is not (AnalysisRunStatus.Cancelled or AnalysisRunStatus.InvalidSource or AnalysisRunStatus.Failed))
            throw new ArgumentOutOfRangeException(nameof(status));
        return new(Id, AuthorizationId, QueueOrder, PlanVersion, SourceRevision, status, Steps, CompletedSteps);
    }
}
