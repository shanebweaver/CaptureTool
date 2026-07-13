using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Infrastructure.Metrics;
using CaptureTool.Infrastructure.Storage;
using System.Text.Json.Serialization.Metadata;

namespace CaptureTool.Infrastructure.Tests.Metrics;

[TestClass]
public sealed class LocalAppMetricsServiceTests
{
    [TestMethod]
    public void Metrics_BeforeInitialize_Throws()
    {
        var service = new LocalAppMetricsService(new TestClock(), new TestJsonStorageService(), new TestLogService());

        Assert.ThrowsExactly<InvalidOperationException>(() => _ = service.Metrics);
    }

    [TestMethod]
    public async Task InitializeAsync_WhenFileMissing_CreatesAndStoresDefaultMetrics()
    {
        var clock = new TestClock { UtcNow = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc) };
        var jsonStorage = new TestJsonStorageService();
        var service = new LocalAppMetricsService(clock, jsonStorage, new TestLogService());

        await service.InitializeAsync(@"C:\CaptureTool\AppData\Metrics.json", TestContext.CancellationToken);

        Assert.AreEqual(clock.UtcNow, service.Metrics.InstallDateUtc);
        Assert.AreEqual(clock.UtcNow, service.Metrics.StoreReviewReminderStartDateUtc);
        Assert.IsTrue(service.Metrics.StoreReviewRemindersEnabled);
        Assert.AreEqual(0, service.Metrics.AppLaunchCount);
        Assert.IsNotNull(jsonStorage.WrittenMetrics);
        Assert.AreEqual(@"C:\CaptureTool\AppData\Metrics.json", jsonStorage.WrittenFilePath);
    }

    [TestMethod]
    public async Task RecordAppLaunchAsync_IncrementsLaunchCountAndSaves()
    {
        var storedMetrics = new AppMetrics
        {
            InstallDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            StoreReviewReminderStartDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            AppLaunchCount = 4
        };
        var jsonStorage = new TestJsonStorageService { ReadResult = storedMetrics };
        var service = new LocalAppMetricsService(new TestClock(), jsonStorage, new TestLogService());
        await service.InitializeAsync("Metrics.json", TestContext.CancellationToken);

        await service.RecordAppLaunchAsync(TestContext.CancellationToken);

        Assert.AreEqual(5, service.Metrics.AppLaunchCount);
        Assert.AreEqual(5, jsonStorage.WrittenMetrics?.AppLaunchCount);
    }

    [TestMethod]
    public async Task ShouldShowStoreReviewReminder_WhenThirtyDaysAndFiveLaunchesPassed_ReturnsTrue()
    {
        var service = await CreateInitializedServiceAsync(
            utcNow: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            appLaunchCount: 5);

        Assert.IsTrue(service.ShouldShowStoreReviewReminder());
    }

    [TestMethod]
    public async Task ShouldShowStoreReviewReminder_BeforeThirtyDaysOrFiveLaunches_ReturnsFalse()
    {
        var beforeThirtyDays = await CreateInitializedServiceAsync(
            utcNow: new DateTime(2026, 1, 30, 0, 0, 0, DateTimeKind.Utc),
            appLaunchCount: 5);
        var beforeFiveLaunches = await CreateInitializedServiceAsync(
            utcNow: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            appLaunchCount: 4);

        Assert.IsFalse(beforeThirtyDays.ShouldShowStoreReviewReminder());
        Assert.IsFalse(beforeFiveLaunches.ShouldShowStoreReviewReminder());
    }

    [TestMethod]
    public async Task RemindAboutStoreReviewLaterAsync_ResetsReminderCriteria()
    {
        var clock = new TestClock { UtcNow = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) };
        var jsonStorage = new TestJsonStorageService
        {
            ReadResult = CreateStoredMetrics(appLaunchCount: 5)
        };
        var service = new LocalAppMetricsService(clock, jsonStorage, new TestLogService());
        await service.InitializeAsync("Metrics.json", TestContext.CancellationToken);

        await service.RemindAboutStoreReviewLaterAsync(TestContext.CancellationToken);

        Assert.IsFalse(service.ShouldShowStoreReviewReminder());
        Assert.AreEqual(clock.UtcNow, service.Metrics.StoreReviewReminderStartDateUtc);
        Assert.AreEqual(5, service.Metrics.StoreReviewReminderStartLaunchCount);
        Assert.AreEqual(5, jsonStorage.WrittenMetrics?.StoreReviewReminderStartLaunchCount);
    }

    [TestMethod]
    public async Task SetStoreReviewRemindersEnabledAsync_WhenDisabled_PreventsPrompt()
    {
        var service = await CreateInitializedServiceAsync(
            utcNow: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            appLaunchCount: 5);

        await service.SetStoreReviewRemindersEnabledAsync(false, TestContext.CancellationToken);

        Assert.IsFalse(service.StoreReviewRemindersEnabled);
        Assert.IsFalse(service.ShouldShowStoreReviewReminder());
    }

    public TestContext TestContext { get; set; } = null!;

    private static async Task<LocalAppMetricsService> CreateInitializedServiceAsync(DateTime utcNow, int appLaunchCount)
    {
        var jsonStorage = new TestJsonStorageService
        {
            ReadResult = CreateStoredMetrics(appLaunchCount)
        };
        var service = new LocalAppMetricsService(new TestClock { UtcNow = utcNow }, jsonStorage, new TestLogService());
        await service.InitializeAsync("Metrics.json", CancellationToken.None);
        return service;
    }

    private static AppMetrics CreateStoredMetrics(int appLaunchCount)
    {
        return new AppMetrics
        {
            InstallDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            StoreReviewReminderStartDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            AppLaunchCount = appLaunchCount,
            StoreReviewReminderStartLaunchCount = 0,
            StoreReviewRemindersEnabled = true
        };
    }

    private sealed class TestClock : IClock
    {
        public DateTime Now => UtcNow.ToLocalTime();
        public DateTime UtcNow { get; init; } = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    }

    private sealed class TestJsonStorageService : IJsonStorageService
    {
        public AppMetrics? ReadResult { get; init; }
        public AppMetrics? WrittenMetrics { get; private set; }
        public string? WrittenFilePath { get; private set; }

        public Task<T?> ReadAsync<T>(FileReference file, JsonTypeInfo<T> jsonTypeInfo)
        {
            return Task.FromResult((T?)(object?)ReadResult);
        }

        public Task WriteAsync<T>(FileReference file, T value, JsonTypeInfo<T> jsonTypeInfo)
        {
            WrittenFilePath = file.FilePath;
            WrittenMetrics = (AppMetrics)(object)value!;
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogService : ILogService
    {
        public event EventHandler<ILogEntry>? LogAdded
        {
            add { }
            remove { }
        }

        public bool IsEnabled => true;

        public void ClearLogs() { }
        public void Disable() { }
        public void Enable() { }
        public IEnumerable<ILogEntry> GetLogs() => [];
        public void LogException(Exception e, string? message = null) { }
        public void LogInformation(string info) { }
        public void LogWarning(string warning) { }
    }
}
