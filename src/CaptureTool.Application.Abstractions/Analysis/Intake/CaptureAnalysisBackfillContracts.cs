namespace CaptureTool.Application.Abstractions.Analysis.Intake;

public readonly record struct CaptureAnalysisBackfillProgress
{
    public CaptureAnalysisBackfillProgress(
        long checkpoint,
        long upperSequence,
        int scheduledCaptureCount)
    {
        if (checkpoint < 0 || upperSequence < checkpoint || scheduledCaptureCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkpoint),
                "Backfill progress must be monotonic and non-negative.");
        }

        Checkpoint = checkpoint;
        UpperSequence = upperSequence;
        ScheduledCaptureCount = scheduledCaptureCount;
    }

    public long Checkpoint { get; }

    public long UpperSequence { get; }

    public int ScheduledCaptureCount { get; }

    public double Fraction => UpperSequence == 0
        ? 1
        : Math.Clamp((double)Checkpoint / UpperSequence, 0, 1);
}

public enum CaptureAnalysisBackfillRunStatus
{
    Unknown,
    Completed,
    AlreadyCompleted,
    Cancelled,
    NotAuthorized,
    FeatureDisabled,
    Conflict,
    Unavailable,
}

public sealed record CaptureAnalysisBackfillRunResult
{
    public CaptureAnalysisBackfillRunResult(
        CaptureAnalysisBackfillRunStatus status,
        CaptureAnalysisBackfillProgress progress)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisBackfillRunStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if ((status is CaptureAnalysisBackfillRunStatus.Completed or
            CaptureAnalysisBackfillRunStatus.AlreadyCompleted) &&
            progress.Checkpoint != progress.UpperSequence)
        {
            throw new ArgumentException(
                "A completed backfill result requires a completed checkpoint.",
                nameof(progress));
        }

        Status = status;
        Progress = progress;
    }

    public CaptureAnalysisBackfillRunStatus Status { get; }

    public CaptureAnalysisBackfillProgress Progress { get; }
}

public interface ICaptureAnalysisBackfillService
{
    Task<CaptureAnalysisBackfillRunResult> RunAsync(
        IProgress<CaptureAnalysisBackfillProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
