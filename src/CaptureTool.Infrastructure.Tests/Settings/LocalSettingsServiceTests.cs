using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Settings.Definitions;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Infrastructure.Settings;
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

    public TestContext TestContext { get; set; } = null!;

    private sealed class TestJsonStorageService : IJsonStorageService
    {
        public List<SettingDefinition>? ReadResult { get; init; }
        public List<SettingDefinition>? WrittenSettings { get; private set; }
        public Exception? WriteException { get; init; }
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }

        public Task<T?> ReadAsync<T>(IFile file, JsonTypeInfo<T> jsonTypeInfo)
        {
            ReadCount++;
            return Task.FromResult((T?)(object?)ReadResult);
        }

        public Task WriteAsync<T>(IFile file, T value, JsonTypeInfo<T> jsonTypeInfo)
        {
            WriteCount++;
            if (WriteException is not null)
            {
                throw WriteException;
            }

            WrittenSettings = (List<SettingDefinition>)(object)value!;
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
        public string? LastMessage { get; private set; }

        public void ClearLogs() { }
        public void Disable() { }
        public void Enable() { }
        public IEnumerable<ILogEntry> GetLogs() => [];
        public void LogException(Exception e, string? message = null) => LastMessage = message;
        public void LogInformation(string info) { }
        public void LogWarning(string warning) { }
    }
}
