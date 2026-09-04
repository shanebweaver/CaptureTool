using CaptureTool.Application.Abstractions.Analysis.Models;
using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.Tests;

[TestClass]
public sealed class FoundryLocalAiModelStorageServiceTests
{
    [TestMethod]
    public async Task RemoveDownloadedModelsAsync_ShouldLeaseBothEnginesAndUseSdkRemoval()
    {
        string root = CreateRoot();
        try
        {
            string modelFolder = Path.Combine(
                root,
                "CaptureAnalysis",
                "FoundryLocal",
                "models");
            Directory.CreateDirectory(modelFolder);
            string modelFile = Path.Combine(modelFolder, "weights.bin");
            File.WriteAllBytes(modelFile, new byte[4096]);

            var nemotronLease = new FakeMaintenanceLeaseSource();
            var whisperLease = new FakeMaintenanceLeaseSource();
            var firstModel = new FakeSdkModel(() =>
            {
                Assert.IsTrue(nemotronLease.IsHeld);
                Assert.IsTrue(whisperLease.IsHeld);
                File.Delete(modelFile);
            });
            var secondModel = new FakeSdkModel(() =>
            {
                Assert.IsTrue(nemotronLease.IsHeld);
                Assert.IsTrue(whisperLease.IsHeld);
            });
            var sdk = new FakeSdkClient([firstModel, secondModel]);
            var provenance = new FakeProvenanceStore();
            var service = new FoundryLocalAiModelStorageService(
                sdk,
                [nemotronLease, whisperLease],
                provenance,
                new StubPathProvider(root));

            AiModelStorageSnapshot before = await service.GetSnapshotAsync(
                TestContext.CancellationToken);
            AiModelStorageRemovalResult result = await service
                .RemoveDownloadedModelsAsync(TestContext.CancellationToken);

            Assert.AreEqual(4096, before.DownloadedByteCount);
            Assert.IsTrue(before.MeasurementSucceeded);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(2, result.RemovedModelCount);
            Assert.AreEqual(4096, result.ReclaimedByteCount);
            Assert.AreEqual(0, result.RemainingByteCount);
            Assert.AreEqual(1, sdk.InitializeCalls);
            Assert.AreEqual(1, sdk.GetCachedModelsCalls);
            Assert.AreEqual(1, firstModel.RemoveCalls);
            Assert.AreEqual(1, secondModel.RemoveCalls);
            Assert.IsFalse(nemotronLease.IsHeld);
            Assert.IsFalse(whisperLease.IsHeld);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    FoundryLocalSpeechModelConfiguration.Whisper.ModelAlias,
                    FoundryLocalSpeechModelConfiguration.NemotronMultilingual.ModelAlias,
                },
                provenance.DeletedAliases);
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task RemoveDownloadedModelsAsync_WhenSdkRemovalFails_ShouldReportFailure()
    {
        string root = CreateRoot();
        try
        {
            var sdk = new FakeSdkClient([
                new FakeSdkModel(() => throw new IOException("model is in use")),
            ]);
            var service = new FoundryLocalAiModelStorageService(
                sdk,
                [new FakeMaintenanceLeaseSource(), new FakeMaintenanceLeaseSource()],
                new FakeProvenanceStore(),
                new StubPathProvider(root));

            AiModelStorageRemovalResult result = await service
                .RemoveDownloadedModelsAsync(TestContext.CancellationToken);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(0, result.RemovedModelCount);
            Assert.AreEqual(1, result.FailedModelCount);
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    public TestContext TestContext { get; set; }

    private static string CreateRoot()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "CaptureToolFoundryModelStorageTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteRoot(string root)
    {
        try
        {
            string resolvedRoot = Path.GetFullPath(root);
            string expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "CaptureToolFoundryModelStorageTests"));
            if (resolvedRoot.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(resolvedRoot))
            {
                Directory.Delete(resolvedRoot, recursive: true);
            }
        }
        catch
        {
            // Test-created model files are safe for the temp scavenger to remove later.
        }
    }

    private sealed class FakeMaintenanceLeaseSource :
        IFoundryLocalSpeechModelMaintenanceLeaseSource
    {
        public bool IsHeld { get; private set; }

        public ValueTask<IAsyncDisposable> AcquireModelMaintenanceLeaseAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.IsFalse(IsHeld);
            IsHeld = true;
            return ValueTask.FromResult<IAsyncDisposable>(new Lease(this));
        }

        private sealed class Lease(FakeMaintenanceLeaseSource owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                owner.IsHeld = false;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeSdkClient(IReadOnlyList<IFoundryLocalSdkModel> cachedModels) :
        IFoundryLocalSdkClient
    {
        public int InitializeCalls { get; private set; }

        public int GetCachedModelsCalls { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public IReadOnlyList<FoundryLocalExecutionProvider> DiscoverExecutionProviders() => [];

        public Task<FoundryLocalExecutionProviderDownloadResult>
            DownloadAndRegisterExecutionProvidersAsync(
                IEnumerable<string> providerNames,
                Action<string, double>? progress,
                CancellationToken cancellationToken) =>
            Task.FromResult(new FoundryLocalExecutionProviderDownloadResult(true, [], []));

        public Task<IFoundryLocalSdkModel?> GetModelAsync(
            string modelAlias,
            FoundryLocalModelDevicePreference devicePreference,
            CancellationToken cancellationToken) =>
            Task.FromResult<IFoundryLocalSdkModel?>(null);

        public Task<IFoundryLocalSdkModel?> GetCachedModelAsync(
            FoundryLocalModelProvenance provenance,
            CancellationToken cancellationToken) =>
            Task.FromResult<IFoundryLocalSdkModel?>(null);

        public Task<IReadOnlyList<IFoundryLocalSdkModel>> GetCachedModelsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCachedModelsCalls++;
            return Task.FromResult(cachedModels);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeSdkModel(Action onRemove) : IFoundryLocalSdkModel
    {
        public FoundryLocalModelProvenance Provenance { get; } = new(
            "test-model",
            "test-model-cpu",
            "1",
            "CPU",
            "CPUExecutionProvider",
            $"sha256:{new string('a', 64)}");

        public int RemoveCalls { get; private set; }

        public Task<bool> IsCachedAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task DownloadAsync(
            Action<float>? progress,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UnloadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveFromCacheAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoveCalls++;
            onRemove();
            return Task.CompletedTask;
        }

        public Task<FoundryLocalAudioTranscription> TranscribeAsync(
            string audioFilePath,
            string languageHint,
            FoundryLocalSpeechTranscriptionMode transcriptionMode,
            CancellationToken cancellationToken) =>
            Task.FromResult(new FoundryLocalAudioTranscription(string.Empty, [], null));
    }

    private sealed class FakeProvenanceStore : IFoundryLocalModelProvenanceStore
    {
        public List<string> DeletedAliases { get; } = [];

        public FoundryLocalModelProvenance? TryRead(string requestedAlias) => null;

        public void Write(FoundryLocalModelProvenance provenance)
        {
        }

        public void Delete(string requestedAlias) => DeletedAliases.Add(requestedAlias);
    }

    private sealed class StubPathProvider(string root) : IApplicationLocalCachePathProvider
    {
        public string GetApplicationLocalCacheFolderPath() => root;
    }
}
