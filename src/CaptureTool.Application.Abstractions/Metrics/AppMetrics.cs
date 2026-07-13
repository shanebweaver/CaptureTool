namespace CaptureTool.Application.Abstractions.Metrics;

public sealed record AppMetrics
{
    public DateTime InstallDateUtc { get; init; }
    public int AppLaunchCount { get; init; }
    public bool StoreReviewRemindersEnabled { get; init; } = true;
    public DateTime StoreReviewReminderStartDateUtc { get; init; }
    public int StoreReviewReminderStartLaunchCount { get; init; }
}
