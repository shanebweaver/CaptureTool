using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.Definitions;
using CaptureTool.Infrastructure.Telemetry;

namespace CaptureTool.Infrastructure.Tests.Telemetry;

[TestClass]
public sealed class TelemetryContextTests
{
    [TestMethod]
    public async Task InitializeAsync_WhenInstallIdMissing_GeneratesAndSavesAnonymousInstallId()
    {
        var settings = new TestSettingsService();
        var context = new TelemetryContext(settings);

        await context.InitializeAsync(TestContext.CancellationToken);

        Assert.IsFalse(context.IsTelemetryEnabled);
        Assert.IsFalse(string.IsNullOrWhiteSpace(settings.InstallId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(context.InstallIdHash));
        Assert.AreNotEqual(settings.InstallId, context.InstallIdHash);
        Assert.AreEqual(1, settings.SaveCount);
    }

    [TestMethod]
    public async Task InitializeAsync_WhenTelemetryEnabled_ReadsOptInSetting()
    {
        var settings = new TestSettingsService
        {
            IsTelemetryEnabled = true,
            InstallId = "existing-install-id"
        };
        var context = new TelemetryContext(settings);

        await context.InitializeAsync(TestContext.CancellationToken);

        Assert.IsTrue(context.IsTelemetryEnabled);
        Assert.AreEqual(0, settings.SaveCount);
    }

    [TestMethod]
    public async Task GetGlobalAttributes_IncludesRequiredContext()
    {
        var settings = new TestSettingsService { InstallId = "existing-install-id" };
        var context = new TelemetryContext(settings);
        await context.InitializeAsync(TestContext.CancellationToken);
        context.SetCurrentRoute("Home");

        IReadOnlyDictionary<string, object?> attributes = context.GetGlobalAttributes();

        Assert.AreEqual("CaptureTool", attributes["app.name"]);
        Assert.AreEqual(context.SessionId, attributes["session.id"]);
        Assert.AreEqual(context.InstallIdHash, attributes["install.id_hash"]);
        Assert.AreEqual("Home", attributes["route.current"]);
        Assert.AreEqual("1", attributes["telemetry.schema_version"]);
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class TestSettingsService : ISettingsService
    {
        public bool IsTelemetryEnabled { get; set; }
        public string InstallId { get; set; } = string.Empty;
        public int SaveCount { get; private set; }

        public event Action<ISettingDefinition[]>? SettingsChanged;

        public T Get<T>(ISettingDefinitionWithValue<T> settingDefinition)
        {
            if (settingDefinition.Key == CaptureToolSettings.Settings_Telemetry_IsEnabled.Key)
            {
                return (T)(object)IsTelemetryEnabled;
            }

            if (settingDefinition.Key == CaptureToolSettings.Settings_Telemetry_InstallId.Key)
            {
                return (T)(object)InstallId;
            }

            return settingDefinition.Value;
        }

        public bool IsSet(ISettingDefinition settingDefinition) => false;

        public void Set(IBoolSettingDefinition settingDefinition, bool value)
        {
            if (settingDefinition.Key == CaptureToolSettings.Settings_Telemetry_IsEnabled.Key)
            {
                IsTelemetryEnabled = value;
                SettingsChanged?.Invoke([settingDefinition]);
            }
        }

        public void Set(IDoubleSettingDefinition settingDefinition, double value)
        {
        }

        public void Set(IIntSettingDefinition settingDefinition, int value)
        {
        }

        public void Set(IStringSettingDefinition settingDefinition, string value)
        {
            if (settingDefinition.Key == CaptureToolSettings.Settings_Telemetry_InstallId.Key)
            {
                InstallId = value;
                SettingsChanged?.Invoke([settingDefinition]);
            }
        }

        public void Unset(ISettingDefinition settingDefinition)
        {
        }

        public void Unset(ISettingDefinition[] settingDefinitions)
        {
        }

        public Task InitializeAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(true);
        }

        public void ClearAllSettings()
        {
        }
    }
}
