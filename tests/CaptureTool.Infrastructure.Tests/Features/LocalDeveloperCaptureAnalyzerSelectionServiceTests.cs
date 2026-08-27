#if DEBUG
using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.Definitions;
using CaptureTool.Application.Analysis.Analyzers;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Features;

namespace CaptureTool.Infrastructure.Tests.Features;

[TestClass]
public sealed class LocalDeveloperCaptureAnalyzerSelectionServiceTests
{
    [TestMethod]
    public async Task SaveAsync_Prefer_PersistsSelectionAndRaisesItsResolutionPreference()
    {
        var settings = new TestSettingsService();
        FakeAnalyzer preferred = CreateAnalyzer("preferred", qualityTier: 1);
        FakeAnalyzer fallback = CreateAnalyzer("fallback", qualityTier: 100);
        var catalog = new CaptureAnalyzerCatalog([fallback, preferred]);
        var service = new LocalDeveloperCaptureAnalyzerSelectionService(settings, catalog);
        var selection = new CaptureAnalyzerSelection(
            AnalysisCapabilities.MediaPropertiesV1,
            CaptureAnalyzerSelectionMode.Prefer,
            new CaptureAnalyzerSelectionTarget("windows", "preferred"));

        CaptureAnalyzerSelectionSaveResult result = await service.SaveAsync(
            [selection],
            CancellationToken.None);
        var reloaded = new LocalDeveloperCaptureAnalyzerSelectionService(settings, catalog);

        Assert.AreEqual(CaptureAnalyzerSelectionSaveStatus.Saved, result.Status);
        Assert.AreEqual(1L, reloaded.Revision);
        Assert.AreEqual(selection, reloaded.GetSelection(AnalysisCapabilities.MediaPropertiesV1));
        Assert.AreEqual(10_000, reloaded.GetPreference(preferred.Descriptor));
        Assert.AreEqual(0, reloaded.GetPreference(fallback.Descriptor));
        Assert.IsTrue(reloaded.IsAllowed(preferred.Descriptor));
        Assert.IsTrue(reloaded.IsAllowed(fallback.Descriptor));
        Assert.IsTrue(reloaded.GetFeatureEnabledOverride(preferred.Descriptor.Identity));
        Assert.IsNull(reloaded.GetFeatureEnabledOverride(fallback.Descriptor.Identity));
    }

    [TestMethod]
    public async Task SaveAsync_Force_AllowsOnlySelectedAnalyzerAndDoesNotRewriteUnchangedState()
    {
        var settings = new TestSettingsService();
        FakeAnalyzer selected = CreateAnalyzer("selected", qualityTier: 1);
        FakeAnalyzer fallback = CreateAnalyzer("fallback", qualityTier: 100);
        var service = new LocalDeveloperCaptureAnalyzerSelectionService(
            settings,
            new CaptureAnalyzerCatalog([fallback, selected]));
        var selection = new CaptureAnalyzerSelection(
            AnalysisCapabilities.MediaPropertiesV1,
            CaptureAnalyzerSelectionMode.Force,
            new CaptureAnalyzerSelectionTarget("windows", "selected"));

        CaptureAnalyzerSelectionSaveResult saved = await service.SaveAsync(
            [selection],
            CancellationToken.None);
        CaptureAnalyzerSelectionSaveResult unchanged = await service.SaveAsync(
            [selection],
            CancellationToken.None);

        Assert.AreEqual(CaptureAnalyzerSelectionSaveStatus.Saved, saved.Status);
        Assert.AreEqual(CaptureAnalyzerSelectionSaveStatus.Unchanged, unchanged.Status);
        Assert.AreEqual(1, settings.SaveCount);
        Assert.IsTrue(service.IsAllowed(selected.Descriptor));
        Assert.IsFalse(service.IsAllowed(fallback.Descriptor));
        Assert.IsTrue(service.GetFeatureEnabledOverride(selected.Descriptor.Identity));
        Assert.IsFalse(service.GetFeatureEnabledOverride(fallback.Descriptor.Identity));
    }

    [TestMethod]
    public async Task SaveAsync_Off_DisablesCapabilityAndAutomaticClearsIt()
    {
        var settings = new TestSettingsService();
        FakeAnalyzer analyzer = CreateAnalyzer("analyzer", qualityTier: 1);
        var service = new LocalDeveloperCaptureAnalyzerSelectionService(
            settings,
            new CaptureAnalyzerCatalog([analyzer]));

        await service.SaveAsync(
            [new CaptureAnalyzerSelection(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureAnalyzerSelectionMode.Off)],
            CancellationToken.None);

        Assert.IsFalse(service.IsAllowed(analyzer.Descriptor));
        Assert.IsFalse(service.GetFeatureEnabledOverride(analyzer.Descriptor.Identity));

        CaptureAnalyzerSelectionSaveResult reset = await service.SaveAsync(
            [CaptureAnalyzerSelection.Automatic(AnalysisCapabilities.MediaPropertiesV1)],
            CancellationToken.None);

        Assert.AreEqual(CaptureAnalyzerSelectionSaveStatus.Saved, reset.Status);
        Assert.AreEqual(
            CaptureAnalyzerSelectionMode.Automatic,
            service.GetSelection(AnalysisCapabilities.MediaPropertiesV1).Mode);
        Assert.IsTrue(service.IsAllowed(analyzer.Descriptor));
        Assert.IsNull(service.GetFeatureEnabledOverride(analyzer.Descriptor.Identity));
        Assert.AreEqual(2L, service.Revision);
    }

    [TestMethod]
    public async Task SaveAsync_RejectsUnknownTargetWithoutChangingStoredPolicy()
    {
        var settings = new TestSettingsService();
        FakeAnalyzer analyzer = CreateAnalyzer("known", qualityTier: 1);
        var service = new LocalDeveloperCaptureAnalyzerSelectionService(
            settings,
            new CaptureAnalyzerCatalog([analyzer]));
        var invalid = new CaptureAnalyzerSelection(
            AnalysisCapabilities.MediaPropertiesV1,
            CaptureAnalyzerSelectionMode.Prefer,
            new CaptureAnalyzerSelectionTarget("windows", "missing"));

        CaptureAnalyzerSelectionSaveResult result = await service.SaveAsync(
            [invalid],
            CancellationToken.None);

        Assert.AreEqual(CaptureAnalyzerSelectionSaveStatus.InvalidSelection, result.Status);
        Assert.AreEqual(0, settings.SaveCount);
        Assert.AreEqual(0L, service.Revision);
        Assert.AreEqual(
            CaptureAnalyzerSelectionMode.Automatic,
            service.GetSelection(AnalysisCapabilities.MediaPropertiesV1).Mode);
    }

    private static FakeAnalyzer CreateAnalyzer(string id, int qualityTier)
    {
        var identity = new AnalyzerIdentity(
            id,
            "windows",
            null,
            null,
            "1",
            null,
            null,
            null,
            null);
        return new(new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.MediaPropertiesV1,
            identity,
            [CaptureMediaKind.Image],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            CaptureAnalyzerRequirement.None,
            CaptureAnalyzerWorkloadClass.Lightweight,
            maximumSourceBytes: null,
            qualityTier));
    }

    private sealed class FakeAnalyzer(CaptureAnalyzerDescriptor descriptor) : ICaptureAnalyzer
    {
        public CaptureAnalyzerDescriptor Descriptor { get; } = descriptor;

        public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
            CaptureAnalyzerAvailabilityRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CaptureAnalyzerAvailability.Available);

        public Task<CaptureAnalyzerOutput> AnalyzeAsync(
            CaptureAnalysisRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestSettingsService : ISettingsService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public event Action<ISettingDefinition[]>? SettingsChanged;

        public int SaveCount { get; private set; }

        public T Get<T>(ISettingDefinitionWithValue<T> settingDefinition) =>
            _values.TryGetValue(settingDefinition.Key, out string? value)
                ? (T)(object)value
                : settingDefinition.Value;

        public bool IsSet(ISettingDefinition settingDefinition) =>
            _values.ContainsKey(settingDefinition.Key);

        public void Set(IStringSettingDefinition settingDefinition, string value) =>
            _values[settingDefinition.Key] = value;

        public async Task<SettingsMutationResult> TrySetAndSaveAsync(
            IStringSettingDefinition settingDefinition,
            string value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Set(settingDefinition, value);
            SaveCount++;
            SettingsChanged?.Invoke([settingDefinition]);
            await Task.CompletedTask;
            return SettingsMutationResult.Saved;
        }

        public void Set(IBoolSettingDefinition settingDefinition, bool value) =>
            throw new NotSupportedException();

        public void Set(IDoubleSettingDefinition settingDefinition, double value) =>
            throw new NotSupportedException();

        public void Set(IIntSettingDefinition settingDefinition, int value) =>
            throw new NotSupportedException();

        public void Unset(ISettingDefinition settingDefinition) =>
            throw new NotSupportedException();

        public void Unset(ISettingDefinition[] settingDefinitions) =>
            throw new NotSupportedException();

        public Task<SettingsMutationResult> TrySetAndSaveAsync(
            IBoolSettingDefinition settingDefinition,
            bool value,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingsMutationResult> TrySetAndSaveAsync(
            IDoubleSettingDefinition settingDefinition,
            double value,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingsMutationResult> TrySetAndSaveAsync(
            IIntSettingDefinition settingDefinition,
            int value,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingsMutationResult> TryUnsetAndSaveAsync(
            ISettingDefinition settingDefinition,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingsMutationResult> TryClearAllAndSaveAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task InitializeAsync(string filePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TrySaveAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void ClearAllSettings() => throw new NotSupportedException();
    }
}
#endif
