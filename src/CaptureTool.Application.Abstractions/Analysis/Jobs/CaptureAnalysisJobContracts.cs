using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Jobs;

public readonly record struct CaptureAnalysisJobLeaseToken
{
    public CaptureAnalysisJobLeaseToken(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A job lease token cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static CaptureAnalysisJobLeaseToken New()
    {
        return new(Guid.NewGuid());
    }
}

public sealed record CaptureAnalysisJobKey
{
    public CaptureAnalysisJobKey(
        AnalysisCommitPreconditions preconditions,
        CapabilityDefinition capability,
        ProcessingBoundary authorizedProcessingBoundary)
    {
        if (preconditions.CaptureId.IsEmpty)
        {
            throw new ArgumentException("A job requires complete processing preconditions.", nameof(preconditions));
        }

        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("A job requires exactly one capability.", nameof(capability));
        }

        if (!Enum.IsDefined(authorizedProcessingBoundary) ||
            authorizedProcessingBoundary == ProcessingBoundary.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(authorizedProcessingBoundary));
        }

        Preconditions = preconditions;
        Capability = capability;
        AuthorizedProcessingBoundary = authorizedProcessingBoundary;
    }

    public AnalysisCommitPreconditions Preconditions { get; }

    public CapabilityDefinition Capability { get; }

    public ProcessingBoundary AuthorizedProcessingBoundary { get; }

    public CaptureId CaptureId => Preconditions.CaptureId;

    public SourceRevision SourceRevision => Preconditions.SourceRevision;
}

public enum CaptureAnalysisJobState
{
    Unknown,
    Pending,
    Running,
    WaitingForCapability,
    RetryScheduled,
    Completed,
    Cancelled,
    TerminalFailure,
}

public enum CaptureAnalyzerAttemptStatus
{
    Unknown,
    Succeeded,
    Unsupported,
    TransientFailure,
    TerminalFailure,
    Cancelled,
}

public sealed record CaptureAnalyzerAttempt
{
    public CaptureAnalyzerAttempt(
        int attemptNumber,
        AnalyzerIdentity analyzer,
        ProcessingBoundary processingBoundary,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        CaptureAnalyzerAttemptStatus status,
        AnalysisFailure? failure)
    {
        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        }

        ArgumentNullException.ThrowIfNull(analyzer);
        if (!Enum.IsDefined(processingBoundary) || processingBoundary == ProcessingBoundary.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(processingBoundary));
        }

        if (startedAtUtc.Offset != TimeSpan.Zero || completedAtUtc.Offset != TimeSpan.Zero ||
            completedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("Attempt timestamps must be ordered UTC values.");
        }

        if (!Enum.IsDefined(status) || status == CaptureAnalyzerAttemptStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool needsFailure = status is CaptureAnalyzerAttemptStatus.Unsupported or
            CaptureAnalyzerAttemptStatus.TransientFailure or CaptureAnalyzerAttemptStatus.TerminalFailure;
        if (needsFailure != failure.HasValue)
        {
            throw new ArgumentException("The attempt status and bounded failure must agree.", nameof(failure));
        }

        if (failure is { } boundedFailure &&
            ((status == CaptureAnalyzerAttemptStatus.TransientFailure &&
              boundedFailure.Disposition != AnalysisFailureDisposition.Transient) ||
             (status is CaptureAnalyzerAttemptStatus.Unsupported or CaptureAnalyzerAttemptStatus.TerminalFailure &&
              boundedFailure.Disposition != AnalysisFailureDisposition.Terminal)))
        {
            throw new ArgumentException("The attempt failure disposition does not match its status.", nameof(failure));
        }

        AttemptNumber = attemptNumber;
        Analyzer = analyzer;
        ProcessingBoundary = processingBoundary;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Status = status;
        Failure = failure;
    }

    public int AttemptNumber { get; }

    public AnalyzerIdentity Analyzer { get; }

    public AnalyzerRevision AnalyzerRevision => Analyzer.Revision;

    public ProcessingBoundary ProcessingBoundary { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public CaptureAnalyzerAttemptStatus Status { get; }

    public AnalysisFailure? Failure { get; }
}

public sealed record CaptureAnalysisJobIntent
{
    private readonly IReadOnlyList<CaptureAnalyzerAttempt> _attempts;

    public CaptureAnalysisJobIntent(
        CaptureAnalysisJobKey key,
        CaptureAnalysisJobState state,
        int attemptCount,
        DateTimeOffset enqueuedAtUtc,
        DateTimeOffset? nextAttemptAtUtc,
        AnalysisFailure? latestFailure,
        IEnumerable<CaptureAnalyzerAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!Enum.IsDefined(state) || state == CaptureAnalysisJobState.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (attemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptCount));
        }

        if (enqueuedAtUtc.Offset != TimeSpan.Zero ||
            nextAttemptAtUtc is { Offset: var offset } && offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Job timestamps must be expressed in UTC.");
        }

        if (latestFailure is { IsEmpty: true })
        {
            throw new ArgumentException("A job cannot retain an empty failure.", nameof(latestFailure));
        }

        ArgumentNullException.ThrowIfNull(attempts);
        CaptureAnalyzerAttempt[] copiedAttempts = [.. attempts];
        if (copiedAttempts.Any(attempt => attempt == null) || copiedAttempts.Length != attemptCount)
        {
            throw new ArgumentException(
                "The attempt count must match a non-null attempt history.",
                nameof(attempts));
        }

        for (int index = 0; index < copiedAttempts.Length; index++)
        {
            CaptureAnalyzerAttempt attempt = copiedAttempts[index];
            if (attempt.AttemptNumber != index + 1 ||
                attempt.ProcessingBoundary != key.AuthorizedProcessingBoundary)
            {
                throw new ArgumentException(
                    "Attempts must be contiguous and remain within the job's authorized boundary.",
                    nameof(attempts));
            }
        }

        bool isRetry = state == CaptureAnalysisJobState.RetryScheduled;
        if (isRetry != nextAttemptAtUtc.HasValue)
        {
            throw new ArgumentException("Only retry-scheduled jobs have a next-attempt time.");
        }

        if (nextAttemptAtUtc < enqueuedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(nextAttemptAtUtc));
        }

        if (isRetry && latestFailure?.Disposition != AnalysisFailureDisposition.Transient)
        {
            throw new ArgumentException("A scheduled retry requires a transient failure.", nameof(latestFailure));
        }

        if (state == CaptureAnalysisJobState.TerminalFailure &&
            latestFailure?.Disposition != AnalysisFailureDisposition.Terminal)
        {
            throw new ArgumentException("A terminal job requires a terminal failure.", nameof(latestFailure));
        }

        if (state is CaptureAnalysisJobState.Completed or CaptureAnalysisJobState.Cancelled &&
            latestFailure.HasValue)
        {
            throw new ArgumentException("Completed and cancelled jobs cannot retain a failure.", nameof(latestFailure));
        }

        if (state == CaptureAnalysisJobState.Pending && latestFailure.HasValue)
        {
            throw new ArgumentException("A pending job cannot retain a failure.", nameof(latestFailure));
        }

        CaptureAnalyzerAttempt? latestAttempt = copiedAttempts.LastOrDefault();
        if (latestAttempt != null &&
            ((state == CaptureAnalysisJobState.RetryScheduled &&
              (latestAttempt.Status != CaptureAnalyzerAttemptStatus.TransientFailure ||
               latestAttempt.Failure != latestFailure)) ||
             (state == CaptureAnalysisJobState.TerminalFailure &&
              (latestAttempt.Status is not (CaptureAnalyzerAttemptStatus.Unsupported or
                  CaptureAnalyzerAttemptStatus.TerminalFailure) ||
               latestAttempt.Failure != latestFailure)) ||
             (state == CaptureAnalysisJobState.Completed &&
              latestAttempt.Status != CaptureAnalyzerAttemptStatus.Succeeded)))
        {
            throw new ArgumentException(
                "The latest attempt does not agree with the durable job state.",
                nameof(attempts));
        }

        Key = key;
        State = state;
        AttemptCount = attemptCount;
        EnqueuedAtUtc = enqueuedAtUtc;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LatestFailure = latestFailure;
        _attempts = Array.AsReadOnly(copiedAttempts);
    }

    public CaptureAnalysisJobKey Key { get; }

    public CaptureAnalysisJobState State { get; }

    public int AttemptCount { get; }

    public DateTimeOffset EnqueuedAtUtc { get; }

    public DateTimeOffset? NextAttemptAtUtc { get; }

    public AnalysisFailure? LatestFailure { get; }

    public IReadOnlyList<CaptureAnalyzerAttempt> Attempts => _attempts;
}

public sealed record CaptureAnalysisJobLease
{
    public CaptureAnalysisJobLease(
        CaptureAnalysisJobLeaseToken leaseToken,
        CaptureAnalysisJobIntent intent,
        DateTimeOffset expiresAtUtc)
    {
        if (leaseToken.IsEmpty)
        {
            throw new ArgumentException("A job lease requires a token.", nameof(leaseToken));
        }

        ArgumentNullException.ThrowIfNull(intent);
        if (intent.State != CaptureAnalysisJobState.Running)
        {
            throw new ArgumentException("A leased job must be running.", nameof(intent));
        }

        if (expiresAtUtc.Offset != TimeSpan.Zero || expiresAtUtc <= intent.EnqueuedAtUtc)
        {
            throw new ArgumentException("A lease expiry must be a later UTC timestamp.", nameof(expiresAtUtc));
        }

        LeaseToken = leaseToken;
        Intent = intent;
        ExpiresAtUtc = expiresAtUtc;
    }

    public CaptureAnalysisJobLeaseToken LeaseToken { get; }

    public CaptureAnalysisJobIntent Intent { get; }

    public DateTimeOffset ExpiresAtUtc { get; }
}

public enum CaptureAnalysisJobEnqueueStatus
{
    Unknown,
    Enqueued,
    AlreadyExists,
    Rejected,
    Unavailable,
}

public sealed record CaptureAnalysisJobEnqueueResult
{
    public CaptureAnalysisJobEnqueueResult(
        CaptureAnalysisJobEnqueueStatus status,
        CaptureAnalysisJobIntent? intent = null)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisJobEnqueueStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool requiresIntent = status is
            CaptureAnalysisJobEnqueueStatus.Enqueued or
            CaptureAnalysisJobEnqueueStatus.AlreadyExists;
        if (requiresIntent != (intent != null))
        {
            throw new ArgumentException("The enqueue status and returned intent must agree.", nameof(intent));
        }

        Status = status;
        Intent = intent;
    }

    public CaptureAnalysisJobEnqueueStatus Status { get; }

    public CaptureAnalysisJobIntent? Intent { get; }
}

public enum CaptureAnalysisJobMutationStatus
{
    Unknown,
    Succeeded,
    NotFound,
    LeaseLost,
    StaleIntent,
    InvalidTransition,
    Unavailable,
}

public sealed record CaptureAnalysisJobMutationResult
{
    public CaptureAnalysisJobMutationResult(
        CaptureAnalysisJobMutationStatus status,
        CaptureAnalysisJobIntent? intent = null)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisJobMutationStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (status == CaptureAnalysisJobMutationStatus.Succeeded && intent == null)
        {
            throw new ArgumentException("A successful job mutation requires the updated intent.", nameof(intent));
        }

        Status = status;
        Intent = intent;
    }

    public CaptureAnalysisJobMutationStatus Status { get; }

    public CaptureAnalysisJobIntent? Intent { get; }
}

public interface ICaptureAnalysisJobStore
{
    ValueTask<CaptureAnalysisJobIntent?> GetAsync(
        CaptureAnalysisJobKey key,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<CaptureAnalysisJobIntent> ReadAllAsync(
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobEnqueueResult> TryEnqueueAsync(
        CaptureAnalysisJobKey key,
        DateTimeOffset enqueuedAtUtc,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobEnqueueResult> TryRequeueAsync(
        CaptureAnalysisJobKey key,
        DateTimeOffset enqueuedAtUtc,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobLease?> TryLeaseNextDueAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    ValueTask<DateTimeOffset?> GetNextDueTimeAsync(
        CancellationToken cancellationToken = default);

    ValueTask<int> RecoverExpiredLeasesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobMutationResult> TryRenewLeaseAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobMutationResult> TryRecordAttemptAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        CaptureAnalyzerAttempt attempt,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobMutationResult> TryScheduleRetryAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        AnalysisFailure failure,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobMutationResult> TryWaitForCapabilityAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        AnalysisFailure? reason,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobMutationResult> TryCompleteAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobMutationResult> TryFailTerminalAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        AnalysisFailure failure,
        CancellationToken cancellationToken = default);

    ValueTask<int> ResumeWaitingForCapabilityAsync(
        CapabilityDefinition capability,
        ProcessingBoundary processingBoundary,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisJobMutationResult> TryCancelAsync(
        CaptureAnalysisJobKey key,
        CancellationToken cancellationToken = default);

    ValueTask<int> CancelCaptureAsync(
        CaptureId captureId,
        long minimumTombstoneGeneration,
        CancellationToken cancellationToken = default);

    ValueTask<int> CancelBeforeControlGenerationAsync(
        long controlGeneration,
        CancellationToken cancellationToken = default);
}
