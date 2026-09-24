using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Analysis;
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
    public async Task SharedOnUseConsentIsPersistedOnceAndScanningOffPreservesIt()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        int prompts = 0;
        app.Prompts.Override = (prompt, _) => { if (prompt == CaptureMemoryPrompt.Consent) prompts++; return Task.FromResult(prompt == CaptureMemoryPrompt.Consent); };
        Assert.IsTrue(await app.Memory.EnsureConsentAsync(Ct));
        Assert.IsFalse(app.Memory.State.Policy.ScanningEnabled);
        Assert.IsTrue(await app.Memory.EnsureConsentAsync(Ct));
        await app.Memory.SetScanningAsync(true, Ct);
        await app.Memory.SetScanningAsync(false, Ct);
        Assert.IsTrue(app.Memory.State.ConsentAvailable);
        Assert.IsTrue(app.Memory.State.Policy.ConsentGranted);
        Assert.IsTrue(await app.Memory.EnsureConsentAsync(Ct));
        Assert.AreEqual(1, prompts);
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
        await app.Memory.SetScanningAsync(true, Ct);
        Assert.IsFalse(app.Memory.State.CanScan);
        Assert.IsFalse(app.Memory.State.ConsentAvailable);
        app.Prompts.Override = (prompt, _) => Task.FromResult(prompt == CaptureMemoryPrompt.Consent);
        Assert.IsTrue(await app.Memory.EnsureConsentAsync(Ct));
    }

    [TestMethod]
    public async Task ConsentAloneDoesNotEnableScanningAndDeclinedHistoryStaysUnscheduledAfterRestart()
    {
        using var environment = new AnalysisTestEnvironment();
        CaptureAsset old;
        await using (var app = new App(environment))
        {
            await app.Memory.InitializeAsync(Ct);
            Assert.IsFalse(app.Memory.State.Policy.IsAllowed);
            old = app.Asset();
            await app.CaptureAsync(old, Ct);
            await app.Memory.SetConsentAsync(true, Ct);
            Assert.IsTrue(app.Memory.State.Policy.ConsentGranted);
            Assert.IsFalse(app.Memory.State.CanScan);
            await app.Memory.ScanExistingAsync(Ct);
            Assert.IsNull(await app.Store.GetWorkAsync(old.Id, Ct));
            await app.EnableAsync(Ct);
            CaptureAsset next = app.Asset();
            await app.CaptureAsync(next, Ct);
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
    public async Task StartupRecoversMissingAdmissionOnceAndPreservesCompletedRunIdentity()
    {
        using var environment = new AnalysisTestEnvironment();
        CaptureAsset asset;
        await using (var app = new App(environment))
        {
            await app.EnableAsync(Ct);
            asset = app.Asset();
            await app.Catalog.RegisterForAnalysisAsync(asset, app.Memory.CaptureAuthorization, Ct);
        }
        Guid run;
        await using (var restarted = new App(environment))
        {
            await restarted.Memory.InitializeAsync(Ct);
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
    public async Task DisableAndReenableRejectDelayedCaptureAndKeepPriorMetadataWhenDeletionDeclined()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        Guid? oldAuthorization = app.Memory.CaptureAuthorization;
        CaptureAsset existing = app.Asset();
        await app.CaptureAsync(existing, Ct);
        await app.CompletedAsync(existing, Ct);
        await app.Memory.SetScanningAsync(false, Ct);
        Assert.IsNull(app.Memory.CaptureAuthorization);
        Assert.IsTrue(app.Memory.State.CanDelete);
        await app.Memory.SetScanningAsync(true, Ct);
        CaptureAsset delayed = app.Asset();
        await app.Memory.RegisterCaptureAsync(delayed, oldAuthorization, Ct);
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
            await app.CaptureAsync(old, Ct);
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
        await restarted.CaptureAsync(next, Ct);
        await restarted.CompletedAsync(next, Ct);
        Assert.AreEqual(revision, restarted.Memory.CaptureAuthorization);
    }

    [TestMethod]
    public async Task EveryConfirmedDeleteRemovesAllIncludingAnalysisCreatedAfterFailedCleanup()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        CaptureAsset old = app.Asset();
        await app.CaptureAsync(old, Ct);
        await app.CompletedAsync(old, Ct);
        app.Prompts.Delete = true;
        environment.Files.FailCleanup = true;
        await app.Memory.DeleteMetadataAsync(Ct);
        Assert.IsTrue(app.Memory.State.Storage.CleanupPending);
        Assert.IsTrue(app.Memory.State.CanDelete, "Failed cleanup must not leave deletion disabled.");
        Assert.AreEqual("cleanup-pending", app.Memory.State.FailureCode);
        CaptureAsset newer = app.Asset();
        await app.CaptureAsync(newer, Ct);
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
        await app.CaptureAsync(asset, Ct);
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
        Task enabling = app.Memory.SetScanningAsync(true, Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await app.Memory.SetScanningAsync(false, Ct);
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
            Task capture = app.CaptureAsync(app.Asset(), Ct);
            Task policy = Task.CompletedTask;
            Task metadata = Task.CompletedTask;
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
                policy = revokeConsent ? app.Memory.SetConsentAsync(false, Ct) : app.Memory.SetScanningAsync(false, Ct);
                app.Prompts.Override = (_, _) => Task.FromResult(false);
                metadata = laterAction switch
                {
                    "delete" => app.Memory.DeleteMetadataAsync(Ct),
                    "declined-consent" => app.Memory.SetConsentAsync(true, Ct),
                    "declined-enable" => app.Memory.SetScanningAsync(true, Ct),
                    "disable" => app.Memory.SetScanningAsync(false, Ct),
                    _ => app.Memory.ScanExistingAsync(Ct)
                };
                Assert.IsNull(app.Memory.CaptureAuthorization);
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
        Assert.IsNull(restarted.Memory.CaptureAuthorization);
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
                enabling = app.Memory.SetScanningAsync(true, Ct);
        };
        Task disabling = app.Memory.SetScanningAsync(false, Ct);
        Assert.IsNotNull(enabling);
        await Task.WhenAll(disabling, enabling);
        Assert.IsTrue(app.Memory.State.CanScan);
        Assert.IsNotNull(app.Memory.CaptureAuthorization);
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
        await app.Memory.SetScanningAsync(false, Ct);
        Assert.IsNull(app.Memory.CaptureAuthorization);
        Assert.IsFalse(app.Memory.State.PolicyAvailable);
        Assert.AreEqual("policy-save", app.Memory.State.FailureCode);
        CaptureAsset blocked = app.Asset();
        await app.CaptureAsync(blocked, Ct);
        Assert.IsNull(await app.Store.GetWorkAsync(blocked.Id, Ct));
        environment.Files.BeforeWrite = null;
        await app.Memory.SetScanningAsync(true, Ct);
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
        await app.CaptureAsync(asset, Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await File.WriteAllTextAsync(environment.ControlPath, "corrupt", Ct);
        release.TrySetResult();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (app.Worker.Progress.Activity != AnalysisActivity.StorageUnavailable) await Task.Delay(10, timeout.Token);
        await app.Memory.RefreshAsync(Ct);
        Assert.IsTrue(app.Memory.State.CanDelete);
        Assert.IsFalse(app.Memory.State.Storage.IsAvailable);
        await app.Memory.ScanExistingAsync(Ct);
        app.Prompts.Delete = true;
        await app.Memory.DeleteMetadataAsync(Ct);
        Assert.IsTrue(app.Memory.State.Storage.IsAvailable);
        app.Analyzer.BeforeComplete = null;
        CaptureAsset next = app.Asset();
        await app.CaptureAsync(next, Ct);
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
            await app.CaptureAsync(asset, Ct);
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
        Assert.IsNull(restarted.Memory.CaptureAuthorization);
        Assert.AreEqual("corrupt", await File.ReadAllTextAsync(Path.Combine(environment.Root, "CaptureMemoryPolicy.bin"), Ct));
    }

    [TestMethod]
    public async Task ManualScanImportsOnlyCapturedExistingRecentsAndDeduplicatesSavedAliases()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        CaptureAsset original = app.Asset();
        await app.CaptureAsync(original, Ct);
        await app.CompletedAsync(original, Ct);
        CaptureAsset saved = app.Asset();
        await app.Memory.SetPreferredPathAsync(original.SourcePath, saved.SourcePath, Ct);
        CaptureAsset historical = app.Asset();
        CaptureAsset opened = app.Asset();
        app.Recents.Entries = [Entry(saved, RecentCaptureOrigin.Captured), Entry(historical, RecentCaptureOrigin.Captured),
            Entry(opened, RecentCaptureOrigin.Opened), new(Path.Combine(environment.Root, "missing.png"), CaptureFileType.Image, RecentCaptureOrigin.Captured, DateTime.UtcNow)];
        await app.Memory.ScanExistingAsync(Ct);
        var imported = (await app.Catalog.ReadAllAsync(Ct)).Single(item => item.SourcePath == historical.SourcePath);
        await app.CompletedAsync(imported, Ct);
        Assert.HasCount(2, await app.Catalog.ReadAllAsync(Ct));
        Assert.AreEqual(CaptureSourceOwnership.External, imported.SourceOwnership);
        await app.Memory.ScanExistingAsync(Ct);
        Assert.HasCount(2, await app.Catalog.ReadAllAsync(Ct));
        Assert.AreEqual(original.SourcePath, (await app.Catalog.GetAsync(original.Id, Ct))!.SourcePath);
    }

    [TestMethod]
    public async Task DisablingInterruptsBackfillBetweenAdmissions()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.Memory.InitializeAsync(Ct);
        for (int i = 0; i < 12; i++) await app.CaptureAsync(app.Asset(), Ct);
        await app.Memory.SetScanningAsync(true, Ct);
        var entered = Signal();
        var release = Signal();
        environment.Files.BeforeWrite = async (path, ct) =>
        {
            if (!path.EndsWith(".analysis", StringComparison.Ordinal)) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
        };
        Task scan = app.Memory.ScanExistingAsync(Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Task disabling = app.Memory.SetScanningAsync(false, Ct);
        Assert.IsNull(app.Memory.CaptureAuthorization);
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
        public Prompts Prompts { get; } = new();
        public Recents Recents { get; } = new();
        public Analyzer Analyzer { get; } = new();
        public App(AnalysisTestEnvironment environment)
        {
            _environment = environment;
            Store = environment.CreateStore();
            Catalog = environment.CreateCatalog();
            Authorization = new(new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files));
            var configuration = new CaptureAnalysisConfiguration([new(AnalysisMediaKind.Image, "test-v1",
                [new(AnalysisCapability.Description, ["test"], TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5))])]);
            Worker = new(Store, Authorization, new LocalAnalysisSource(), configuration, [Analyzer], Catalog);
            Memory = new(Authorization, Catalog, Store, Worker, Prompts, Recents, new LocalFileSystem());
        }
        public CaptureAsset Asset()
        {
            Directory.CreateDirectory(_environment.Root);
            string path = Path.Combine(_environment.Root, Guid.NewGuid() + ".png");
            File.WriteAllText(path, "retained source media");
            return new(CaptureId.New(), CaptureFileType.Image, DateTimeOffset.UtcNow, path, CaptureSourceOwnership.Application);
        }
        public Task CaptureAsync(CaptureAsset asset, CancellationToken ct) => Memory.RegisterCaptureAsync(asset, Memory.CaptureAuthorization, ct);
        public async Task EnableAsync(CancellationToken ct) { await Memory.InitializeAsync(ct); await Memory.SetScanningAsync(true, ct); }
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
            Memory.Dispose(); Worker.Dispose(); Authorization.Dispose(); Catalog.Dispose(); Store.Dispose();
        }
    }
    private sealed class Prompts : ICaptureMemoryPrompts
    {
        public bool Delete { get; set; }
        public int DeleteCalls { get; private set; }
        public Func<CaptureMemoryPrompt, CancellationToken, Task<bool>>? Override { get; set; }
        public Task<bool> ConfirmAsync(CaptureMemoryPrompt prompt, CancellationToken ct)
        {
            if (prompt == CaptureMemoryPrompt.DeleteMetadata) DeleteCalls++;
            return Override?.Invoke(prompt, ct) ?? Task.FromResult(prompt == CaptureMemoryPrompt.Consent || prompt == CaptureMemoryPrompt.DeleteMetadata && Delete);
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
