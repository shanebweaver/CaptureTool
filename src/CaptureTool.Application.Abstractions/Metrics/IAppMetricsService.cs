namespace CaptureTool.Application.Abstractions.Metrics;

public interface IAppMetricsService
{
    AppMetrics Metrics { get; }
    bool StoreReviewRemindersEnabled { get; }

    Task InitializeAsync(string filePath, CancellationToken cancellationToken);
    Task RecordAppLaunchAsync(CancellationToken cancellationToken);
    bool ShouldShowStoreReviewReminder();
    Task RemindAboutStoreReviewLaterAsync(CancellationToken cancellationToken);
    Task SetStoreReviewRemindersEnabledAsync(bool isEnabled, CancellationToken cancellationToken);
}
