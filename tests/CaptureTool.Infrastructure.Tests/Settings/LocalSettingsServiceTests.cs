using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.Definitions;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Infrastructure.Settings;
using CaptureTool.Infrastructure.Storage;
using System.Text.Json.Serialization.Metadata;

namespace CaptureTool.Infrastructure.Tests.Settings;

[TestClass]
public sealed class LocalSettingsServiceTests
{
    [TestMethod]
    public void Get_BeforeInitialize_Throws()
    {
        using var service = new LocalSettingsService(new TestLogService(), new TestJsonStorageService());

        Assert.ThrowsExactly<InvalidOperationException>(() => service.Get(new BoolSettingDefinition("enabled", true)));
    }

    [TestMethod]
    public async Task InitializeAsync_LoadsSettingsAndRaisesChangedEvent()
    {
        var storedSetting = new BoolSettingDefinition("enabled", false);
        var jsonStorage = new TestJsonStorageService { ReadResult = [storedSetting] };
        using var service = new LocalSettingsService(new TestLogService(), jsonStorage);
        ISettingDefinition[]? changed = null;
        service.SettingsChanged += settings => changed = settings;

        await service.InitializeAsync("settings.json", TestContext.CancellationToken);

        Assert.IsFalse(service.Get(new BoolSettingDefinition("enabled", true)));
        Assert.IsNotNull(changed);
        Assert.AreEqual("enabled", changed[0].Key);
    }

    [TestMethod]
    public async Task Set_WhenValueChanges_StoresSettingAndRaisesChangedEvent()
    {
        using var service = new LocalSettingsService(new TestLogService(), new TestJsonStorageService());
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        int changedCount = 0;
        service.SettingsChanged += _ => changedCount++;
        var definition = new StringSettingDefinition("folder", "");

        service.Set(definition, @"C:\Captures");
        service.Set(definition, @"C:\Captures");

        Assert.AreEqual(@"C:\Captures", service.Get(definition));
        Assert.IsTrue(service.IsSet(definition));
        Assert.AreEqual(1, changedCount);
    }

    [TestMethod]
    public async Task Set_TracksOnlyAllowListedSettingsWithSafeValues()
    {
        var telemetry = new RecordingTelemetryService();
        using var service = new LocalSettingsService(
            new TestLogService(),
            new TestJsonStorageService(),
            telemetry);
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);

        service.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, false);
        service.Set(
            CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder,
            @"C:\Private\Screenshots");

        Assert.HasCount(1, telemetry.Events);
        var trackedEvent = telemetry.Events.Single();
        Assert.AreEqual(TelemetryEvents.SettingsChanged, trackedEvent.Name);
        Assert.AreEqual(
            CaptureToolSettings.Settings_ImageCapture_AutoCopy.Key,
            trackedEvent.Properties[TelemetryProperties.Setting]);
        Assert.IsFalse((bool)trackedEvent.Properties[TelemetryProperties.Value]!);
        Assert.IsFalse(trackedEvent.Properties.Values.Contains(@"C:\Private\Screenshots"));
    }

    [TestMethod]
    public async Task Unset_RemovesSingleSettingAndRaisesChangedEvent()
    {
        using var service = new LocalSettingsService(new TestLogService(), new TestJsonStorageService());
        var definition = new IntSettingDefinition("count", 1);
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        service.Set(definition, 2);
        int changedCount = 0;
        service.SettingsChanged += _ => changedCount++;

        service.Unset(definition);

        Assert.AreEqual(1, service.Get(definition));
        Assert.IsFalse(service.IsSet(definition));
        Assert.AreEqual(1, changedCount);
    }

    [TestMethod]
    public async Task UnsetMany_RaisesChangedOnlyWhenASettingWasRemoved()
    {
        using var service = new LocalSettingsService(new TestLogService(), new TestJsonStorageService());
        var first = new BoolSettingDefinition("first", false);
        var second = new BoolSettingDefinition("second", false);
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        service.Set(first, true);
        int changedCount = 0;
        service.SettingsChanged += _ => changedCount++;

        service.Unset([first, second]);
        service.Unset([second]);

        Assert.IsFalse(service.IsSet(first));
        Assert.AreEqual(1, changedCount);
    }

    [TestMethod]
    public async Task TrySaveAsync_WritesCurrentSettings()
    {
        var jsonStorage = new TestJsonStorageService();
        using var service = new LocalSettingsService(new TestLogService(), jsonStorage);
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        service.Set(new DoubleSettingDefinition("scale", 1), 2.5);

        bool saved = await service.TrySaveAsync(TestContext.CancellationToken);

        Assert.IsTrue(saved);
        Assert.IsNotNull(jsonStorage.WrittenSettings);
        Assert.AreEqual("scale", jsonStorage.WrittenSettings[0].Key);
    }

    [TestMethod]
    public async Task TrySaveAsync_WhenStorageThrows_LogsAndReturnsFalse()
    {
        var logService = new TestLogService();
        var jsonStorage = new TestJsonStorageService { WriteException = new IOException("nope") };
        using var service = new LocalSettingsService(logService, jsonStorage);
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);

        bool saved = await service.TrySaveAsync(TestContext.CancellationToken);

        Assert.IsFalse(saved);
        StringAssert.Contains(logService.LastMessage!, "Unable to perform save operation.");
    }

    [TestMethod]
    public async Task TrySetAndSaveAsync_WhenStorageThrows_KeepsCommittedValueAndSuppressesNotification()
    {
        var logService = new TestLogService();
        var jsonStorage = new TestJsonStorageService { WriteException = new IOException("nope") };
        var telemetry = new RecordingTelemetryService();
        using var service = new LocalSettingsService(logService, jsonStorage, telemetry);
        IBoolSettingDefinition definition = CaptureToolSettings.Settings_ImageCapture_AutoCopy;
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        int changedCount = 0;
        service.SettingsChanged += _ => changedCount++;

        SettingsMutationResult result = await service.TrySetAndSaveAsync(
            definition,
            false,
            TestContext.CancellationToken);

        Assert.AreEqual(SettingsMutationStatus.PersistenceFailed, result.Status);
        Assert.IsTrue(service.Get(definition));
        Assert.IsFalse(service.IsSet(definition));
        Assert.AreEqual(0, changedCount);
        Assert.IsEmpty(telemetry.Events);
        StringAssert.Contains(logService.LastMessage!, "settings mutation save operation");
    }

    [TestMethod]
    public async Task TrySetAndSaveAsync_WhenWriteSucceeds_CommitsThenNotifies()
    {
        var jsonStorage = new TestJsonStorageService();
        var telemetry = new RecordingTelemetryService();
        using var service = new LocalSettingsService(
            new TestLogService(),
            jsonStorage,
            telemetry);
        IBoolSettingDefinition definition = CaptureToolSettings.Settings_ImageCapture_AutoCopy;
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        ISettingDefinition[]? changed = null;
        service.SettingsChanged += settings => changed = settings;

        SettingsMutationResult result = await service.TrySetAndSaveAsync(
            definition,
            false,
            TestContext.CancellationToken);

        Assert.AreEqual(SettingsMutationStatus.Saved, result.Status);
        Assert.IsFalse(service.Get(definition));
        Assert.IsTrue(service.IsSet(definition));
        Assert.IsNotNull(changed);
        Assert.AreEqual(definition.Key, changed.Single().Key);
        Assert.AreEqual(definition.Key, jsonStorage.WrittenSettings!.Single().Key);
        Assert.HasCount(1, telemetry.Events);
    }

    [TestMethod]
    public async Task TryClearAllAndSaveAsync_WhileWriteIsPending_KeepsCommittedValuesReadableThenNotifies()
    {
        var writeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var jsonStorage = new TestJsonStorageService
        {
            WriteStarted = writeStarted,
            ContinueWrite = continueWrite,
        };
        using var service = new LocalSettingsService(new TestLogService(), jsonStorage);
        var first = new BoolSettingDefinition("first", false);
        var second = new StringSettingDefinition("second", "default");
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        service.Set(first, true);
        service.Set(second, "saved");
        ISettingDefinition[]? changed = null;
        service.SettingsChanged += settings => changed = settings;

        Task<SettingsMutationResult> clearTask = service.TryClearAllAndSaveAsync(
            TestContext.CancellationToken);
        await writeStarted.Task;
        bool[] concurrentReads = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() => service.Get(first))));

        Assert.IsTrue(concurrentReads.All(value => value));
        Assert.IsNull(changed);

        continueWrite.SetResult(true);
        SettingsMutationResult result = await clearTask;

        Assert.AreEqual(SettingsMutationStatus.Saved, result.Status);
        Assert.IsFalse(service.Get(first));
        Assert.AreEqual("default", service.Get(second));
        Assert.IsNotNull(changed);
        CollectionAssert.AreEquivalent(
            new[] { "first", "second" },
            changed.Select(setting => setting.Key).ToArray());
        Assert.IsEmpty(jsonStorage.WrittenSettings!);
    }

    [TestMethod]
    public async Task TrySetAndSaveAsync_WhenLaterMutationOccurs_DoesNotOverwriteLaterValue()
    {
        var writeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var jsonStorage = new TestJsonStorageService
        {
            WriteStarted = writeStarted,
            ContinueWrite = continueWrite,
        };
        using var service = new LocalSettingsService(new TestLogService(), jsonStorage);
        var definition = new BoolSettingDefinition("enabled", false);
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);

        Task<SettingsMutationResult> saveTask = service.TrySetAndSaveAsync(
            definition,
            true,
            TestContext.CancellationToken);
        await writeStarted.Task;

        service.Set(definition, false);
        continueWrite.SetResult(true);
        SettingsMutationResult result = await saveTask;

        Assert.AreEqual(SettingsMutationStatus.Saved, result.Status);
        Assert.IsFalse(service.Get(definition));
    }

    [TestMethod]
    public async Task ClearAllSettings_UsesNotificationPathForRemovedSettings()
    {
        using var service = new LocalSettingsService(new TestLogService(), new TestJsonStorageService());
        var first = new BoolSettingDefinition("first", false);
        var second = new IntSettingDefinition("second", 0);
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        service.Set(first, true);
        service.Set(second, 2);
        ISettingDefinition[]? changed = null;
        service.SettingsChanged += settings => changed = settings;

        service.ClearAllSettings();

        Assert.IsNotNull(changed);
        CollectionAssert.AreEquivalent(
            new[] { "first", "second" },
            changed.Select(setting => setting.Key).ToArray());
    }

    [TestMethod]
    public async Task AfterDispose_InitializeAndSaveReturnWithoutStorageAccess()
    {
        var jsonStorage = new TestJsonStorageService();
        var service = new LocalSettingsService(new TestLogService(), jsonStorage);

        service.Dispose();

        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        bool saved = await service.TrySaveAsync(TestContext.CancellationToken);

        Assert.IsFalse(saved);
        Assert.AreEqual(0, jsonStorage.ReadCount);
        Assert.AreEqual(0, jsonStorage.WriteCount);
    }

    [TestMethod]
    public async Task TrySetAndSaveAsync_AfterDispose_ReturnsServiceUnavailable()
    {
        var jsonStorage = new TestJsonStorageService();
        var service = new LocalSettingsService(new TestLogService(), jsonStorage);
        await service.InitializeAsync("settings.json", TestContext.CancellationToken);
        service.Dispose();

        SettingsMutationResult result = await service.TrySetAndSaveAsync(
            new BoolSettingDefinition("enabled", false),
            true,
            TestContext.CancellationToken);

        Assert.AreEqual(SettingsMutationStatus.ServiceUnavailable, result.Status);
        Assert.AreEqual(0, jsonStorage.WriteCount);
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class TestJsonStorageService : IJsonStorageService
    {
        public List<SettingDefinition>? ReadResult { get; init; }
        public List<SettingDefinition>? WrittenSettings { get; private set; }
        public Exception? WriteException { get; set; }
        public TaskCompletionSource<bool>? WriteStarted { get; init; }
        public TaskCompletionSource<bool>? ContinueWrite { get; init; }
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }

        public Task<T?> ReadAsync<T>(FileReference file, JsonTypeInfo<T> jsonTypeInfo)
        {
            ReadCount++;
            return Task.FromResult((T?)(object?)ReadResult);
        }

        public async Task WriteAsync<T>(FileReference file, T value, JsonTypeInfo<T> jsonTypeInfo)
        {
            WriteCount++;
            if (WriteException is not null)
            {
                throw WriteException;
            }

            WriteStarted?.TrySetResult(true);
            if (ContinueWrite is not null)
            {
                await ContinueWrite.Task;
            }

            WrittenSettings = (List<SettingDefinition>)(object)value!;
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
        public string? LastMessage { get; private set; }

        public void ClearLogs() { }
        public void Disable() { }
        public void Enable() { }
        public IEnumerable<ILogEntry> GetLogs() => [];
        public void LogException(Exception e, string? message = null) => LastMessage = message;
        public void LogInformation(string info) { }
        public void LogWarning(string warning) { }
    }

    private sealed class RecordingTelemetryService : ITelemetryService
    {
        public List<(string Name, IReadOnlyDictionary<string, object?> Properties)> Events { get; } = [];

        public void TrackEvent(
            string eventName,
            IReadOnlyDictionary<string, object?>? properties = null)
        {
            Events.Add((eventName, properties ?? new Dictionary<string, object?>()));
        }
    }
}
