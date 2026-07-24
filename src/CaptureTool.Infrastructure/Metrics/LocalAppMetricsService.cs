using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Infrastructure.Metrics.Serialization;
using CaptureTool.Infrastructure.Storage;

namespace CaptureTool.Infrastructure.Metrics;

public sealed class LocalAppMetricsService : IAppMetricsService
{
    private sealed class MetricsFile(string filePath) : FileReference(filePath);

    private static readonly TimeSpan StoreReviewReminderDelay = TimeSpan.FromDays(30);
    private const int StoreReviewReminderLaunchThreshold = 5;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly IClock _clock;
    private readonly IJsonStorageService _jsonStorageService;
    private readonly ILogService _logService;
    private readonly ITelemetryService? _telemetryService;

    private AppMetrics _metrics = new();
    private MetricsFile? _metricsFile;
    private bool _isInitialized;

    public LocalAppMetricsService(
        IClock clock,
        IJsonStorageService jsonStorageService,
        ILogService logService,
        ITelemetryService? telemetryService = null)
    {
        _clock = clock;
        _jsonStorageService = jsonStorageService;
        _logService = logService;
        _telemetryService = telemetryService;
    }

    public AppMetrics Metrics
    {
        get
        {
            ThrowIfNotInitialized();
            return _metrics;
        }
    }

    public bool StoreReviewRemindersEnabled => Metrics.StoreReviewRemindersEnabled;

    public async Task InitializeAsync(string filePath, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_isInitialized)
            {
                return;
            }

            MetricsFile metricsFile = new(filePath);
            AppMetrics? metrics = null;
            try
            {
                metrics = await _jsonStorageService.ReadAsync(metricsFile, AppMetricsContext.Default.AppMetrics);
            }
            catch (Exception e)
            {
                _logService.LogException(e, "Failed to load metrics file.");
            }

            DateTime utcNow = _clock.UtcNow;
            bool shouldSave = false;
            if (metrics == null)
            {
                metrics = CreateDefaultMetrics(utcNow);
                shouldSave = true;
            }
            else if (metrics.InstallDateUtc == default || metrics.StoreReviewReminderStartDateUtc == default)
            {
                metrics = metrics with
                {
                    InstallDateUtc = metrics.InstallDateUtc == default ? utcNow : metrics.InstallDateUtc,
                    StoreReviewReminderStartDateUtc = metrics.StoreReviewReminderStartDateUtc == default
                        ? (metrics.InstallDateUtc == default ? utcNow : metrics.InstallDateUtc)
                        : metrics.StoreReviewReminderStartDateUtc
                };
                shouldSave = true;
            }

            _metrics = metrics;
            _metricsFile = metricsFile;
            _isInitialized = true;

            if (shouldSave)
            {
                await TrySaveAsync(cancellationToken);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task RecordAppLaunchAsync(CancellationToken cancellationToken)
    {
        return UpdateMetricsAsync(
            metrics => metrics with { AppLaunchCount = metrics.AppLaunchCount + 1 },
            cancellationToken);
    }

    public bool ShouldShowStoreReviewReminder()
    {
        ThrowIfNotInitialized();

        DateTime utcNow = _clock.UtcNow;
        int launchesSinceReminderStart = _metrics.AppLaunchCount - _metrics.StoreReviewReminderStartLaunchCount;

        return
            _metrics.StoreReviewRemindersEnabled &&
            utcNow - _metrics.StoreReviewReminderStartDateUtc >= StoreReviewReminderDelay &&
            launchesSinceReminderStart >= StoreReviewReminderLaunchThreshold;
    }

    public async Task RemindAboutStoreReviewLaterAsync(CancellationToken cancellationToken)
    {
        await UpdateMetricsAsync(
            metrics => metrics with
            {
                StoreReviewRemindersEnabled = true,
                StoreReviewReminderStartDateUtc = _clock.UtcNow,
                StoreReviewReminderStartLaunchCount = metrics.AppLaunchCount
            },
            cancellationToken);
        TrackReviewReminderSetting(true, "remind_later");
    }

    public async Task SetStoreReviewRemindersEnabledAsync(bool isEnabled, CancellationToken cancellationToken)
    {
        await UpdateMetricsAsync(
            metrics => metrics with { StoreReviewRemindersEnabled = isEnabled },
            cancellationToken);
        TrackReviewReminderSetting(isEnabled, "setting");
    }

    private async Task UpdateMetricsAsync(Func<AppMetrics, AppMetrics> updateMetrics, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            ThrowIfNotInitialized();

            _metrics = updateMetrics(_metrics);
            await TrySaveAsync(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _jsonStorageService.WriteAsync(GetMetricsFile(), _metrics, AppMetricsContext.Default.AppMetrics);
            return true;
        }
        catch (Exception e)
        {
            _logService.LogException(e, "Unable to save metrics file.");
            return false;
        }
    }

    private MetricsFile GetMetricsFile()
    {
        ThrowIfNotInitialized();

        if (_metricsFile == null)
        {
            throw new InvalidOperationException("AppMetricsService has not been initialized with a file path.");
        }

        return _metricsFile;
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("AppMetricsService must be initialized before it can be queried.");
        }
    }

    private static AppMetrics CreateDefaultMetrics(DateTime utcNow)
    {
        return new AppMetrics
        {
            InstallDateUtc = utcNow,
            StoreReviewReminderStartDateUtc = utcNow
        };
    }

    private void TrackReviewReminderSetting(bool enabled, string source)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.SettingsChanged,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Setting] = "store_review_reminders",
                [TelemetryProperties.Value] = enabled,
                [TelemetryProperties.Source] = source
            });
    }
}
