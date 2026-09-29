using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Analysis;
using CaptureTool.Application.Capture;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.Analysis.Sources;
using CaptureTool.Infrastructure.CaptureAssets;
using CaptureTool.Infrastructure.Files;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class CaptureMemoryIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestMethod]
    [DataRow("png")]
    [DataRow("mp4")]
    public async Task TextScanAdmitsOcrAndQrTogetherWithoutOtherAnalysis(string extension)
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        using var catalog = environment.CreateCatalog();
        using var authorization = new CaptureMemoryAuthorization(new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files));
        var worker = new AdmissionWorker();
        using var memory = new CaptureMemoryService(authorization, catalog, store, worker, new Prompts(), new LocalFileSystem());
        try
        {
            await memory.InitializeAsync(Ct);
            await memory.SetConsentAsync(true, Ct);
            Directory.CreateDirectory(environment.Root);
            string path = Path.Combine(environment.Root, "scan." + extension);
            File.WriteAllText(path, "synthetic source");
            await memory.ScanTextAsync(path, Ct);
            Assert.IsNotNull(worker.Admitted);
            CollectionAssert.AreEqual(new[] { AnalysisCapability.QrCodeDetection, AnalysisCapability.TextRecognition }, worker.Admitted.Capabilities!.ToArray());
            Assert.IsTrue(worker.Admitted.ReuseExisting, "Completed OCR or QR results must be reused on a partial retry.");
        }
        finally { await memory.StopAsync(); }
    }

    [TestMethod]
    public async Task UnavailableSemanticModelDoesNotScheduleItsMediaPrerequisites()
    {
        using var environment = new AnalysisTestEnvironment();
        var unavailable = new ReadinessProcessor();
        await using var app = new App(environment, [unavailable]);
        await app.EnableAsync(Ct);
        var asset = app.Asset();
        await app.Memory.AnalyzeAsync(asset.SourcePath, AnalysisCapability.CaptureSynopsis, Ct);
        Assert.AreEqual("model-unavailable", app.Memory.State.FailureCode);
        Assert.AreEqual(1, unavailable.Probes);
        Assert.AreEqual(0, app.Analyzer.Calls);
        Assert.IsEmpty(await app.Store.ReadPendingAsync(Ct));
        Assert.IsEmpty(await app.Catalog.ReadAllAsync(Ct));
    }

    [TestMethod]
    [DataRow(AnalyzerAvailability.Ready)]
    [DataRow(AnalyzerAvailability.PreparationRequired)]
    public async Task AvailableFallbackAllowsExplicitScreenshotRequestPastPreflight(AnalyzerAvailability readiness)
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        using var catalog = environment.CreateCatalog();
        using var authorization = new CaptureMemoryAuthorization(new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files));
        var unavailable = new ReadinessProcessor();
        var fallback = new ReadinessProcessor("foundry-test", readiness);
        var worker = new AdmissionWorker();
        using var memory = new CaptureMemoryService(authorization, catalog, store, worker, new Prompts(), new LocalFileSystem(),
            processors: [unavailable, fallback]);
        try
        {
            await memory.InitializeAsync(Ct);
            await memory.SetConsentAsync(true, Ct);
            Assert.AreEqual(0, unavailable.Probes);
            Assert.AreEqual(0, fallback.Probes);
            Assert.IsNull(worker.Admitted);
            Directory.CreateDirectory(environment.Root);
            string path = Path.Combine(environment.Root, "explicit.png");
            File.WriteAllText(path, "retained source media");
            await memory.AnalyzeAsync(path, AnalysisCapability.CaptureSynopsis, Ct);
            Assert.IsNull(memory.State.FailureCode);
            Assert.AreEqual(1, unavailable.Probes);
            Assert.AreEqual(1, fallback.Probes);
            Assert.IsNotNull(worker.Admitted);
            CollectionAssert.AreEqual(new[] { AnalysisCapability.TextRecognition, AnalysisCapability.Description, AnalysisCapability.CaptureSynopsis },
                worker.Admitted.Capabilities!.ToArray());
        }
        finally { await memory.StopAsync(); }
    }

    private sealed class AdmissionWorker : ICaptureAnalysisWorker
    {
        public AnalysisRequest? Admitted { get; private set; }
        public AnalysisActivitySnapshot Progress { get; } = new(AnalysisActivity.Idle);
        public event Action<AnalysisActivitySnapshot>? ProgressChanged { add { } remove { } }
        public event Action<CaptureId, AnalysisCapability>? ResultCommitted { add { } remove { } }
        public Task<bool> EnqueueAsync(AnalysisRequest request, CancellationToken ct) { Admitted = request; return Task.FromResult(true); }
        public Task RunAsync(CancellationToken ct) => Task.CompletedTask;
        public Task CancelAsync(CaptureId id, CancellationToken ct) => throw new NotSupportedException();
        public Task<AnalysisCleanupResult> ClearAsync(long boundary, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ReadinessProcessor(string id = "windows-test", AnalyzerAvailability availability = AnalyzerAvailability.Unsupported) : IMetadataProcessor
    {
        public int Probes;
        public MetadataProcessorDescriptor Descriptor { get; } = MetadataEnrichmentConfiguration.CreateSemantic(id, AnalysisCapability.CaptureSynopsis);
        public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(CancellationToken ct) { Probes++; return ValueTask.FromResult(availability); }
        public Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct) => throw new InvalidOperationException("Preflight cannot prepare models.");
        public Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken ct) => throw new InvalidOperationException("Preflight cannot run models.");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task OverlappingFeatureConsentRequestsShareOneDecision(bool approved)
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.Memory.InitializeAsync(Ct);
        int prompts = 0;
        var decision = new TaskCompletionSource<bool>();
        app.Prompts.Override = (_, _) => { prompts++; return decision.Task; };
        Task<bool> first = app.Memory.EnsureConsentAsync(Ct);
        Task<bool> second = app.Memory.EnsureConsentAsync(Ct);
        Assert.AreEqual(1, prompts);
        decision.SetResult(approved);
        Assert.AreEqual(approved, await first);
        Assert.AreEqual(approved, await second);
        Assert.AreEqual(1, prompts);
        Assert.AreEqual(approved, app.Memory.State.Policy.ConsentGranted);
    }

    [TestMethod]
    public async Task SharedOnUseConsentIsPersistedOnceAndRevocationRequiresFreshApproval()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        int prompts = 0;
        app.Prompts.Override = (prompt, _) => { if (prompt == CaptureMemoryPrompt.Consent) prompts++; return Task.FromResult(prompt == CaptureMemoryPrompt.Consent); };
        Assert.IsTrue(await app.Memory.EnsureConsentAsync(Ct));
        Assert.IsFalse(app.Memory.State.Policy.ScanningEnabled);
        Assert.IsTrue(await app.Memory.EnsureConsentAsync(Ct));
        await app.Memory.SetConsentAsync(true, Ct);
        await app.Memory.SetConsentAsync(false, Ct);
        Assert.IsTrue(app.Memory.State.ConsentAvailable);
        Assert.IsFalse(app.Memory.State.Policy.ConsentGranted);
        Assert.IsTrue(await app.Memory.EnsureConsentAsync(Ct));
        Assert.AreEqual(2, prompts);
    }

    [TestMethod]
    public async Task ConsentRevocationBlocksImmediatelyAndFailedWriteRequiresFreshApproval()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        var entered = Signal();
        var release = Signal();
        environment.Files.BeforeWrite = async (path, ct) =>
        {
            if (!path.EndsWith("CaptureMemoryPolicy.bin", StringComparison.Ordinal)) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            throw new IOException("Injected policy failure");
        };
        var revocation = app.Memory.SetConsentAsync(false, Ct);
        await entered.Task.WaitAsync(Ct);
        Assert.IsFalse(app.Memory.State.ConsentAvailable);
        release.TrySetResult();
        await revocation;
        Assert.IsFalse(app.Memory.State.ConsentAvailable);
        environment.Files.BeforeWrite = null;
        app.Prompts.Override = (_, _) => Task.FromResult(false);
        Assert.IsFalse(await app.Memory.EnsureConsentAsync(Ct));
        await app.Memory.SetConsentAsync(true, Ct);
        Assert.IsFalse(app.Memory.State.CanScan);
        Assert.IsFalse(app.Memory.State.ConsentAvailable);
        app.Prompts.Override = (prompt, _) => Task.FromResult(prompt == CaptureMemoryPrompt.Consent);
        Assert.IsTrue(await app.Memory.EnsureConsentAsync(Ct));
    }

    [TestMethod]
    public async Task ConsentDoesNotScheduleHistoryOrResumeWorkAfterRestart()
    {
        using var environment = new AnalysisTestEnvironment();
        CaptureAsset old;
        await using (var app = new App(environment))
        {
            await app.Memory.InitializeAsync(Ct);
            Assert.IsFalse(app.Memory.State.Policy.IsAllowed);
            old = app.Asset();
            await app.RequestAsync(old, Ct);
            await app.Memory.SetConsentAsync(true, Ct);
            Assert.IsTrue(app.Memory.State.Policy.ConsentGranted);
            Assert.IsTrue(app.Memory.State.CanScan);
            Assert.IsNull(await app.Store.GetWorkAsync(old.Id, Ct));
            await app.EnableAsync(Ct);
            CaptureAsset next = app.Asset();
            await app.RequestAsync(next, Ct);
            await app.CompletedAsync(next, Ct);
            Assert.IsNull(await app.Store.GetWorkAsync(old.Id, Ct));
        }
        await using var restarted = new App(environment);
        await restarted.Memory.InitializeAsync(Ct);
        Assert.IsTrue(restarted.Memory.State.CanScan);
        Assert.IsNull(await restarted.Store.GetWorkAsync(old.Id, Ct));
        Assert.HasCount(2, await restarted.Catalog.ReadAllAsync(Ct));
    }

    [TestMethod]
    public async Task ConsentNeverOffersAnAutomaticLibraryScan()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.Memory.InitializeAsync(Ct);
        var missing = app.Asset();
        await app.RequestAsync(missing, Ct);
        File.Delete(missing.SourcePath);
        app.Recents.Entries = [Entry(app.Asset(), RecentCaptureOrigin.Opened), Entry(missing, RecentCaptureOrigin.Captured)];
        var prompts = new List<CaptureMemoryPrompt>();
        app.Prompts.Override = (prompt, _) => { prompts.Add(prompt); return Task.FromResult(true); };
        await app.Memory.SetConsentAsync(true, Ct);
        Assert.IsTrue(app.Memory.State.CanScan);
        CollectionAssert.AreEqual(new[] { CaptureMemoryPrompt.Consent }, prompts);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExistingCapturesWaitForExplicitRequestsAfterConsent(bool recentOnly)
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.Memory.InitializeAsync(Ct);
        var old = app.Asset();
        if (recentOnly) app.Recents.Entries = [Entry(old, RecentCaptureOrigin.Captured)];
        else await app.RequestAsync(old, Ct);
        var prompts = new List<CaptureMemoryPrompt>();
        app.Prompts.Override = (prompt, _) => { prompts.Add(prompt); return Task.FromResult(true); };
        await app.Memory.SetConsentAsync(true, Ct);
        CollectionAssert.AreEqual(new[] { CaptureMemoryPrompt.Consent }, prompts);
        Assert.IsEmpty(await app.Store.ReadPendingAsync(Ct));
        Assert.AreEqual(0, app.Analyzer.Calls);
        await app.Memory.AnalyzeAsync(old.SourcePath, AnalysisCapability.Description, Ct);
        var registered = (await app.Catalog.ReadAllAsync(Ct)).Single();
        await app.CompletedAsync(registered, Ct);
    }

    [TestMethod]
    public async Task ConsentAndCaptureRegistrationNeverScheduleWorkIncludingLegacyAutomaticPolicy()
    {
        using var environment = new AnalysisTestEnvironment();
        var policyStore = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        await policyStore.SaveAsync(new(true, true, Guid.NewGuid(), 0), Ct);
        await using var app = new App(environment);
        await app.Memory.InitializeAsync(Ct);
        var asset = app.Asset();
        await app.Memory.RegisterCaptureAsync(asset, Ct);
        await app.Memory.RefreshAsync(Ct);
        await app.Memory.EnsureConsentAsync(Ct);
        Assert.IsNull(await app.Store.GetWorkAsync(asset.Id, Ct));
        Assert.IsEmpty(await app.Store.ReadPendingAsync(Ct));
        Assert.AreEqual(0, app.Analyzer.Calls);
        await app.Memory.AnalyzeAsync(asset.SourcePath, AnalysisCapability.Description, Ct);
        await app.CompletedAsync(asset, Ct);
        Assert.AreEqual(1, app.Analyzer.Calls);
    }

    [TestMethod]
    public async Task CloseDiscardsActiveAndQueuedScansAndStartupDoesNotReadTheirMetadata()
    {
        using var environment = new AnalysisTestEnvironment();
        CaptureAsset completed, active, queued;
        await using (var app = new App(environment))
        {
            await app.EnableAsync(Ct);
            completed = app.Asset();
            await app.RequestAsync(completed, Ct);
            await app.CompletedAsync(completed, Ct);
            var started = Signal();
            app.Analyzer.BeforeComplete = async ct =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            };
            active = app.Asset(); queued = app.Asset();
            await app.RequestAsync(active, Ct);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            await app.RequestAsync(queued, Ct);
            await app.Memory.StopAsync().WaitAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.IsEmpty(await app.Store.ReadPendingAsync(Ct));
            Assert.AreEqual(AnalysisRunStatus.Cancelled, (await app.Store.GetWorkAsync(active.Id, Ct))!.Run.Status);
            Assert.AreEqual(AnalysisRunStatus.Cancelled, (await app.Store.GetWorkAsync(queued.Id, Ct))!.Run.Status);
        }
        await using var restarted = new App(environment);
        int metadataReads = 0;
        environment.Files.BeforeRead = (path, _) =>
        {
            if (path.EndsWith(".analysis", StringComparison.Ordinal)) Interlocked.Increment(ref metadataReads);
            return Task.CompletedTask;
        };
        await restarted.Memory.InitializeAsync(Ct);
        await restarted.Memory.RefreshAsync(Ct);
        await restarted.Memory.InitializeAsync(Ct);
        Assert.AreEqual(0, metadataReads, "Startup and settings must not walk historical analysis records.");
        Assert.AreEqual(0, restarted.Analyzer.Calls);
        Assert.AreEqual(AnalysisActivity.Idle, restarted.Worker.Progress.Activity);
        Assert.IsEmpty(await restarted.Store.ReadPendingAsync(Ct));
        Assert.IsNotNull(await restarted.Store.GetAsync(completed.Id, cancellationToken: Ct));
        await restarted.Memory.AnalyzeAsync(active.SourcePath, AnalysisCapability.Description, Ct);
        await restarted.CompletedAsync(active, Ct);
        Assert.AreEqual(1, restarted.Analyzer.Calls);
        Assert.AreEqual(AnalysisRunStatus.Cancelled, (await restarted.Store.GetWorkAsync(queued.Id, Ct))!.Run.Status);
        await restarted.Memory.RefreshAsync(Ct);
        Assert.IsNotNull(await restarted.Store.GetAsync(completed.Id, cancellationToken: Ct));
        Assert.AreEqual(1, restarted.Analyzer.Calls, "Reading completed metadata must not repeat inference.");
    }

    [TestMethod]
    public async Task StartupDoesNotRecoverMissingAdmissionsAndExplicitActionsCanAnalyzeThem()
    {
        using var environment = new AnalysisTestEnvironment();
        CaptureAsset asset;
        await using (var app = new App(environment))
        {
            await app.EnableAsync(Ct);
            asset = app.Asset();
            await app.Catalog.RegisterForAnalysisAsync(asset, app.Grant, Ct);
        }
        Guid run;
        await using (var restarted = new App(environment))
        {
            await restarted.Memory.InitializeAsync(Ct);
            Assert.IsNull(await restarted.Store.GetWorkAsync(asset.Id, Ct));
            Assert.AreEqual(0, restarted.Analyzer.Calls);
            await restarted.Memory.AnalyzeAsync(asset.SourcePath, AnalysisCapability.Description, Ct);
            run = (await restarted.CompletedAsync(asset, Ct)).Run.Id;
            await restarted.Memory.InitializeAsync(Ct);
            Assert.AreEqual(run, (await restarted.Store.GetWorkAsync(asset.Id, Ct))!.Run.Id);
        }
        await using var again = new App(environment);
        await again.Memory.InitializeAsync(Ct);
        Assert.AreEqual(run, (await again.Store.GetWorkAsync(asset.Id, Ct))!.Run.Id);
        Assert.AreEqual(0, again.Analyzer.Calls);
    }

    [TestMethod]
    public async Task RegistrationAfterRegrantDoesNotScheduleWorkAndDeclinedDeletionKeepsMetadata()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        CaptureAsset existing = app.Asset();
        await app.RequestAsync(existing, Ct);
        await app.CompletedAsync(existing, Ct);
        await app.Memory.SetConsentAsync(false, Ct);
        Assert.IsNull(app.Grant);
        Assert.IsTrue(app.Memory.State.CanDelete);
        await app.Memory.SetConsentAsync(true, Ct);
        CaptureAsset delayed = app.Asset();
        await app.Memory.RegisterCaptureAsync(delayed, Ct);
        Assert.IsNull(await app.Store.GetWorkAsync(delayed.Id, Ct));
        Assert.IsNotNull(await app.Store.GetAsync(existing.Id, cancellationToken: Ct));
        await app.Memory.SetConsentAsync(false, Ct);
        Assert.IsFalse(app.Memory.State.Policy.ScanningEnabled);
        Assert.IsFalse(app.Memory.State.Policy.ConsentGranted);
        Assert.IsTrue(app.Memory.State.CanDelete);
    }

    [TestMethod]
    public async Task ClearFencesRestartReconciliationButKeepsCatalogPolicyAndMedia()
    {
        using var environment = new AnalysisTestEnvironment();
        CaptureAsset old;
        Guid revision;
        await using (var app = new App(environment))
        {
            await app.EnableAsync(Ct);
            old = app.Asset();
            await app.RequestAsync(old, Ct);
            await app.CompletedAsync(old, Ct);
            revision = app.Memory.State.Policy.Revision;
            app.Prompts.Delete = true;
            await app.Memory.DeleteMetadataAsync(Ct);
            Assert.IsFalse(app.Memory.State.CanDelete);
            Assert.IsTrue(File.Exists(old.SourcePath));
            Assert.AreEqual(revision, app.Memory.State.Policy.Revision);
        }
        await using var restarted = new App(environment);
        await restarted.Memory.InitializeAsync(Ct);
        Assert.IsNull(await restarted.Store.GetWorkAsync(old.Id, Ct));
        Assert.IsNotNull(await restarted.Catalog.GetAsync(old.Id, Ct));
        CaptureAsset next = restarted.Asset();
        await restarted.RequestAsync(next, Ct);
        await restarted.CompletedAsync(next, Ct);
        Assert.AreEqual(revision, restarted.Grant);
    }

    [TestMethod]
    public async Task EveryConfirmedDeleteRemovesAllIncludingAnalysisCreatedAfterFailedCleanup()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        CaptureAsset old = app.Asset();
        await app.RequestAsync(old, Ct);
        await app.CompletedAsync(old, Ct);
        app.Prompts.Delete = true;
        environment.Files.FailCleanup = true;
        await app.Memory.DeleteMetadataAsync(Ct);
        Assert.IsTrue(app.Memory.State.Storage.CleanupPending);
        Assert.IsTrue(app.Memory.State.CanDelete, "Failed cleanup must not leave deletion disabled.");
        Assert.AreEqual("cleanup-pending", app.Memory.State.FailureCode);
        CaptureAsset newer = app.Asset();
        await app.RequestAsync(newer, Ct);
        await app.CompletedAsync(newer, Ct);
        Assert.IsNotNull(await app.Store.GetAsync(newer.Id, cancellationToken: Ct));
        environment.Files.FailCleanup = false;
        await app.Memory.DeleteMetadataAsync(Ct);
        Assert.IsNull(await app.Store.GetWorkAsync(newer.Id, Ct));
        Assert.IsEmpty(environment.MetadataPaths);
        Assert.IsFalse(app.Memory.State.CanDelete);
        Assert.IsFalse(app.Memory.State.IsDeleting);
        Assert.IsTrue(File.Exists(newer.SourcePath));
        Assert.HasCount(2, await app.Catalog.ReadAllAsync(Ct));
    }

    [TestMethod]
    public async Task DeleteIsDisabledWhileRunningAndRecoversAfterPublicationFailure()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        CaptureAsset asset = app.Asset();
        await app.RequestAsync(asset, Ct);
        await app.CompletedAsync(asset, Ct);
        app.Prompts.Delete = true;
        var entered = Signal();
        var release = Signal();
        environment.Files.BeforeWrite = async (path, ct) =>
        {
            if (path != environment.ControlPath) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            throw new IOException("Injected clear publication failure.");
        };
        Task deleting = app.Memory.DeleteMetadataAsync(Ct);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            Assert.IsTrue(app.Memory.State.IsDeleting);
            Assert.IsFalse(app.Memory.State.CanDelete);
            Assert.IsFalse(app.Memory.State.CanScan);
            await app.Memory.DeleteMetadataAsync(Ct); // A repeated click must not supersede the active clear.
            Assert.AreEqual(1, app.Prompts.DeleteCalls);
        }
        finally { release.TrySetResult(); }
        await deleting;
        Assert.IsFalse(app.Memory.State.IsDeleting);
        Assert.IsTrue(app.Memory.State.CanDelete);
        Assert.IsNotNull(await app.Store.GetAsync(asset.Id, cancellationToken: Ct));
        environment.Files.BeforeWrite = null;
        await app.Memory.DeleteMetadataAsync(Ct);
        Assert.IsFalse(app.Memory.State.CanDelete);
    }

    [TestMethod]
    public async Task StaleConsentDialogCannotUndoNewerDisable()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.Memory.InitializeAsync(Ct);
        var entered = Signal();
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Prompts.Override = (prompt, ct) =>
        {
            if (prompt != CaptureMemoryPrompt.Consent) return Task.FromResult(false);
            entered.TrySetResult();
            return answer.Task.WaitAsync(ct);
        };
        Task enabling = app.Memory.SetConsentAsync(true, Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await app.Memory.SetConsentAsync(false, Ct);
        answer.TrySetResult(true);
        await enabling;
        Assert.IsFalse(app.Memory.State.Policy.IsAllowed);
        Assert.IsFalse(app.Memory.State.Policy.ConsentGranted);
    }

    [TestMethod]
    [DataRow(false, "scan")]
    [DataRow(false, "delete")]
    [DataRow(false, "declined-consent")]
    [DataRow(true, "scan")]
    [DataRow(true, "delete")]
    [DataRow(true, "declined-consent")]
    [DataRow(true, "declined-enable")]
    [DataRow(true, "disable")]
    public async Task LaterCommandCannotDiscardQueuedDisableOrRevocation(bool revokeConsent, string laterAction)
    {
        using var environment = new AnalysisTestEnvironment();
        await using (var app = new App(environment))
        {
            await app.EnableAsync(Ct);
            var entered = Signal();
            var release = Signal();
            environment.Files.BeforeWrite = async (path, ct) =>
            {
                if (!path.EndsWith("catalog.bin", StringComparison.Ordinal)) return;
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
            };
            Task capture = app.RequestAsync(app.Asset(), Ct);
            Task policy = Task.CompletedTask;
            Task metadata = Task.CompletedTask;
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
                policy = app.Memory.SetConsentAsync(false, Ct);
                app.Prompts.Override = (_, _) => Task.FromResult(false);
                metadata = laterAction switch
                {
                    "delete" => app.Memory.DeleteMetadataAsync(Ct),
                    "declined-consent" => app.Memory.SetConsentAsync(true, Ct),
                    "declined-enable" => app.Memory.SetConsentAsync(true, Ct),
                    "disable" => app.Memory.SetConsentAsync(false, Ct),
                    _ => app.Memory.AnalyzeAsync(app.Asset().SourcePath, AnalysisCapability.Description, Ct)
                };
                Assert.IsNull(app.Grant);
            }
            finally { release.TrySetResult(); }
            await Task.WhenAll(capture, policy, metadata);
            environment.Files.BeforeWrite = null;
            Assert.IsFalse(app.Memory.State.Policy.ScanningEnabled);
            if (revokeConsent) Assert.IsFalse(app.Memory.State.Policy.ConsentGranted);
        }
        await using var restarted = new App(environment);
        await restarted.Memory.InitializeAsync(Ct);
        Assert.IsFalse(restarted.Memory.State.CanScan);
        Assert.IsNull(restarted.Grant);
    }

    [TestMethod]
    public async Task OlderDisableCannotUndoNewerEnableCommittedDuringItsStateNotification()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        Task? enabling = null;
        int started = 0;
        app.Memory.StateChanged += () =>
        {
            if (!app.Memory.State.PolicyAvailable && Interlocked.Exchange(ref started, 1) == 0)
                enabling = app.Memory.SetConsentAsync(true, Ct);
        };
        Task disabling = app.Memory.SetConsentAsync(false, Ct);
        Assert.IsNotNull(enabling);
        await Task.WhenAll(disabling, enabling);
        Assert.IsTrue(app.Memory.State.CanScan);
        Assert.IsNotNull(app.Grant);
    }

    [TestMethod]
    public async Task RevocationDuringEnablePublicationCannotReopenAuthorization()
    {
        using var environment = new AnalysisTestEnvironment();
        var policyStore = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        using var authorization = new CaptureMemoryAuthorization(policyStore);
        await authorization.InitializeAsync(Ct);
        var entered = Signal();
        var release = Signal();
        environment.Files.BeforeWrite = async (_, ct) => { entered.TrySetResult(); await release.Task.WaitAsync(ct); };
        Task enabling = authorization.SaveAsync(new(true, true, Guid.NewGuid(), 0), Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        authorization.Block();
        release.TrySetResult();
        await enabling;
        Assert.IsFalse(authorization.IsAllowed);
        using var lease = await authorization.AcquireAsync(Ct);
        Assert.IsFalse(lease.IsAllowed);
    }

    [TestMethod]
    public async Task FailedPolicyWriteBlocksSessionUntilExplicitEnableRetry()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        Guid revision = app.Memory.State.Policy.Revision;
        environment.Files.BeforeWrite = (path, _) => path.EndsWith("CaptureMemoryPolicy.bin", StringComparison.Ordinal)
            ? throw new IOException("Injected policy write failure.") : Task.CompletedTask;
        await app.Memory.SetConsentAsync(false, Ct);
        Assert.IsNull(app.Grant);
        Assert.IsFalse(app.Memory.State.PolicyAvailable);
        Assert.AreEqual("policy-save", app.Memory.State.FailureCode);
        CaptureAsset blocked = app.Asset();
        await app.RequestAsync(blocked, Ct);
        Assert.IsNull(await app.Store.GetWorkAsync(blocked.Id, Ct));
        environment.Files.BeforeWrite = null;
        await app.Memory.SetConsentAsync(true, Ct);
        Assert.IsTrue(app.Memory.State.CanScan);
        Assert.AreNotEqual(revision, app.Memory.State.Policy.Revision);
    }

    [TestMethod]
    public async Task CorruptStoreCanBeDeletedAndTheSameWorkerResumesProcessing()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        var entered = Signal();
        var release = Signal();
        app.Analyzer.BeforeComplete = async ct => { entered.TrySetResult(); await release.Task.WaitAsync(ct); };
        CaptureAsset asset = app.Asset();
        await app.RequestAsync(asset, Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await File.WriteAllTextAsync(environment.ControlPath, "corrupt", Ct);
        release.TrySetResult();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (app.Worker.Progress.Activity != AnalysisActivity.StorageUnavailable) await Task.Delay(10, timeout.Token);
        await app.Memory.RefreshAsync(Ct);
        Assert.IsTrue(app.Memory.State.CanDelete);
        Assert.IsFalse(app.Memory.State.Storage.IsAvailable);
        await app.Memory.AnalyzeAsync(asset.SourcePath, AnalysisCapability.Description, Ct);
        app.Prompts.Delete = true;
        await app.Memory.DeleteMetadataAsync(Ct);
        Assert.IsTrue(app.Memory.State.Storage.IsAvailable);
        app.Analyzer.BeforeComplete = null;
        CaptureAsset next = app.Asset();
        await app.RequestAsync(next, Ct);
        await app.CompletedAsync(next, Ct);
        Assert.AreEqual(1, app.Analyzer.MaximumConcurrency);
    }

    [TestMethod]
    public async Task UnreadableConsentBlocksScanningButStillAllowsMetadataDeletion()
    {
        using var environment = new AnalysisTestEnvironment();
        await using (var app = new App(environment))
        {
            await app.EnableAsync(Ct);
            CaptureAsset asset = app.Asset();
            await app.RequestAsync(asset, Ct);
            await app.CompletedAsync(asset, Ct);
        }
        await File.WriteAllTextAsync(Path.Combine(environment.Root, "CaptureMemoryPolicy.bin"), "corrupt", Ct);
        await using var restarted = new App(environment);
        await restarted.Memory.InitializeAsync(Ct);
        Assert.IsFalse(restarted.Memory.State.CanScan);
        Assert.IsTrue(restarted.Memory.State.CanDelete);
        Assert.AreEqual("policy-unavailable", restarted.Memory.State.FailureCode);
        restarted.Prompts.Delete = true;
        await restarted.Memory.DeleteMetadataAsync(Ct);
        Assert.IsFalse(restarted.Memory.State.CanDelete);
        Assert.IsNull(restarted.Grant);
        Assert.AreEqual("corrupt", await File.ReadAllTextAsync(Path.Combine(environment.Root, "CaptureMemoryPolicy.bin"), Ct));
    }

    [TestMethod]
    public async Task ExplicitRequestsDoNotImportOtherRecentsAndSavedAliasesKeepIdentity()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        CaptureAsset original = app.Asset();
        await app.RequestAsync(original, Ct);
        await app.CompletedAsync(original, Ct);
        CaptureAsset saved = app.Asset();
        await app.Memory.SetPreferredPathAsync(original.SourcePath, saved.SourcePath, Ct);
        CaptureAsset historical = app.Asset();
        CaptureAsset opened = app.Asset();
        app.Recents.Entries = [Entry(saved, RecentCaptureOrigin.Captured), Entry(historical, RecentCaptureOrigin.Captured),
            Entry(opened, RecentCaptureOrigin.Opened), new(Path.Combine(environment.Root, "missing.png"), CaptureFileType.Image, RecentCaptureOrigin.Captured, DateTime.UtcNow)];
        await app.Memory.AnalyzeAsync(historical.SourcePath, AnalysisCapability.Description, Ct);
        var imported = (await app.Catalog.ReadAllAsync(Ct)).Single(item => item.SourcePath == historical.SourcePath);
        await app.CompletedAsync(imported, Ct);
        Assert.HasCount(2, await app.Catalog.ReadAllAsync(Ct));
        Assert.AreEqual(CaptureSourceOwnership.External, imported.SourceOwnership);
        Assert.IsNull(imported.CapturedAt, "Recent activity does not establish when a historical capture was taken.");
        await app.Memory.AnalyzeAsync(saved.SourcePath, AnalysisCapability.Description, Ct);
        await app.CompletedAsync(original, Ct);
        Assert.HasCount(2, await app.Catalog.ReadAllAsync(Ct));
        Assert.AreEqual(original.SourcePath, (await app.Catalog.GetAsync(original.Id, Ct))!.SourcePath);
    }

    [TestMethod]
    public async Task DisablingDuringAnOpenedCaptureAdmissionPreventsFurtherWork()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.Memory.InitializeAsync(Ct);
        for (int i = 0; i < 12; i++) await app.RequestAsync(app.Asset(), Ct);
        await app.Memory.SetConsentAsync(true, Ct);
        var entered = Signal();
        var release = Signal();
        environment.Files.BeforeWrite = async (path, ct) =>
        {
            if (!path.EndsWith(".analysis", StringComparison.Ordinal)) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
        };
        Task scan = app.Memory.AnalyzeAsync(app.Asset().SourcePath, AnalysisCapability.Description, Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Task disabling = app.Memory.SetConsentAsync(false, Ct);
        Assert.IsNull(app.Grant);
        release.TrySetResult();
        await Task.WhenAll(scan, disabling);
        Assert.HasCount(1, environment.MetadataPaths);
        Assert.IsFalse(app.Memory.State.IsScheduling);
        Assert.IsFalse(app.Memory.State.CanScan);
    }

    private static RecentCaptureCatalogEntry Entry(CaptureAsset asset, RecentCaptureOrigin origin) =>
        new(asset.SourcePath, asset.MediaType, origin, DateTime.UtcNow);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class App : IAsyncDisposable
    {
        private readonly AnalysisTestEnvironment _environment;
        public LocalCaptureAnalysisStore Store { get; }
        public LocalCaptureAssetCatalog Catalog { get; }
        public CaptureMemoryAuthorization Authorization { get; }
        public CaptureAnalysisWorker Worker { get; }
        public CaptureMemoryService Memory { get; }
        public CaptureNamingService Names { get; }
        public Prompts Prompts { get; } = new();
        public Recents Recents { get; } = new();
        public Analyzer Analyzer { get; } = new();
        public App(AnalysisTestEnvironment environment, IEnumerable<IMetadataProcessor>? processors = null)
        {
            _environment = environment;
            Store = environment.CreateStore();
            Catalog = environment.CreateCatalog();
            Authorization = new(new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files));
            var configuration = new CaptureAnalysisConfiguration([new(AnalysisMediaKind.Image, "test-v1",
                [new(AnalysisCapability.Description, ["test"], TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5))])]);
            Worker = new(Store, Authorization, new LocalAnalysisSource(), configuration, [Analyzer], Catalog);
            Names = new(Catalog, Catalog, new NameNotifier(), new LocalFileSystem());
            Memory = new(Authorization, Catalog, Store, Worker, Prompts, new LocalFileSystem(), Names, processors);
        }
        public CaptureAsset Asset()
        {
            Directory.CreateDirectory(_environment.Root);
            string path = Path.Combine(_environment.Root, Guid.NewGuid() + ".png");
            File.WriteAllText(path, "retained source media");
            return new(CaptureId.New(), CaptureFileType.Image, DateTimeOffset.UtcNow, path, CaptureSourceOwnership.Application);
        }
        public Guid? Grant => Authorization.IsAllowed ? Authorization.Policy.Revision : null;
        public async Task RequestAsync(CaptureAsset asset, CancellationToken ct)
        {
            await Memory.RegisterCaptureAsync(asset, ct);
            await Memory.AnalyzeAsync(asset.SourcePath, AnalysisCapability.Description, ct);
        }
        public async Task EnableAsync(CancellationToken ct) { await Memory.InitializeAsync(ct); await Memory.SetConsentAsync(true, ct); }
        public async Task<AnalysisWorkItem> CompletedAsync(CaptureAsset asset, CancellationToken ct)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            while (true)
            {
                var work = await Store.GetWorkAsync(asset.Id, timeout.Token);
                if (work?.Run.Status == AnalysisRunStatus.Completed) return work;
                await Task.Delay(10, timeout.Token);
            }
        }
        public async ValueTask DisposeAsync()
        {
            await Memory.StopAsync();
            Memory.Dispose(); Names.Dispose(); Worker.Dispose(); Authorization.Dispose(); Catalog.Dispose(); Store.Dispose();
        }
    }
    private sealed class NameNotifier : IRecentCapturesChangeNotifier
    {
        public event EventHandler? RecentCapturesChanged;
        public void NotifyRecentCapturesChanged() => RecentCapturesChanged?.Invoke(this, EventArgs.Empty);
    }
    private sealed class Prompts : ICaptureMemoryPrompts
    {
        public bool Delete { get; set; }
        public int DeleteCalls { get; private set; }
        public Func<CaptureMemoryPrompt, CancellationToken, Task<bool>>? Override { get; set; }
        public Task<bool> ConfirmAsync(CaptureMemoryPrompt prompt, CancellationToken ct)
        {
            if (prompt == CaptureMemoryPrompt.DeleteMetadata) DeleteCalls++;
            return Override?.Invoke(prompt, ct) ?? Task.FromResult(prompt is CaptureMemoryPrompt.Consent || prompt == CaptureMemoryPrompt.DeleteMetadata && Delete);
        }
    }
    private sealed class Analyzer : IMediaAnalyzer
    {
        private int _active;
        public int Calls { get; private set; }
        public int MaximumConcurrency { get; private set; }
        public Func<CancellationToken, Task>? BeforeComplete { get; set; }
        public MediaAnalyzerDescriptor Descriptor { get; } = new("test", AnalysisCapability.Description, [AnalysisMediaKind.Image]);
        public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct) => ValueTask.FromResult(AnalyzerAvailability.Ready);
        public Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct) => Task.FromResult(AnalyzerAvailability.Ready);
        public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
        {
            Calls++;
            MaximumConcurrency = Math.Max(MaximumConcurrency, Interlocked.Increment(ref _active));
            try
            {
                if (BeforeComplete != null) await BeforeComplete(ct);
                return AnalyzerOutcome.Success(new DescriptionMetadata([new("private analysis")]), new("test", "local", "test-model", "1"));
            }
            finally { Interlocked.Decrement(ref _active); }
        }
    }
    private sealed class Recents : IRecentCaptureCatalog
    {
        public IReadOnlyList<RecentCaptureCatalogEntry> Entries { get; set; } = [];
        public IReadOnlyList<RecentCaptureCatalogEntry> GetEntries() => Entries;
        public void RecordCaptured(string path, CaptureFileType type) => throw new NotSupportedException();
        public void RecordOpened(string path, CaptureFileType type) => throw new NotSupportedException();
        public void ReplacePath(string oldPath, string newPath) => throw new NotSupportedException();
        public void Touch(string path) => throw new NotSupportedException();
        public bool Remove(string path) => throw new NotSupportedException();
        public int RemoveRange(IEnumerable<string> paths) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }
}
