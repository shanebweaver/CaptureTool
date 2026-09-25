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
using System.Text;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class CaptureNamingTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestMethod]
    public async Task OnlyNewEnrolledCapturesGainOneNameAndEstablishedNamesSurviveDeletionAndRestart()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new Setup(environment);
        await app.InitializeAsync(Ct);
        var old = await app.CaptureAsync(Ct);
        Assert.IsTrue(await app.Names.SetEnabledAsync(true, Ct));
        var fresh = await app.CaptureAsync(Ct);
        await app.TitleAsync(old, "Historical title", Ct);
        await app.TitleAsync(fresh, "Invoice for September", Ct);
        await app.Names.ReconcileAsync(Ct);
        Assert.IsNull((await app.Catalog.GetAsync(old.Id, Ct))!.Name);
        Assert.AreEqual(new CaptureName("Invoice for September", true), (await app.Catalog.GetAsync(fresh.Id, Ct))!.Name);
        await app.TitleAsync(fresh, "Changed model suggestion", Ct);
        await app.Names.ReconcileAsync(Ct);
        await app.Names.InvalidatePendingAsync(Ct);
        await app.Store.ClearAsync(await app.Catalog.GetBoundaryAsync(Ct), Ct);
        using var reopened = environment.CreateCatalog();
        Assert.AreEqual("Invoice for September", (await reopened.GetAsync(fresh.Id, Ct))!.Name!.Text);
        Assert.IsTrue(File.Exists(fresh.SourcePath));
        foreach (var bytes in environment.Files.PublishedBytes)
            Assert.DoesNotContain("Invoice for September", Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public async Task UserNameWinsBeforeAndAfterAutomaticPublication()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new Setup(environment);
        await app.InitializeAsync(Ct);
        await app.Names.SetEnabledAsync(true, Ct);
        var asset = await app.CaptureAsync(Ct);
        await app.TitleAsync(asset, "Automatic name", Ct);
        Assert.IsTrue(await app.Names.SetNameAsync(asset.SourcePath, asset.MediaType, "My own name", Ct));
        await app.Names.ReconcileAsync(Ct);
        Assert.AreEqual(new CaptureName("My own name", false), (await app.Catalog.GetAsync(asset.Id, Ct))!.Name);
        var next = await app.CaptureAsync(Ct);
        await app.TitleAsync(next, "Automatic name", Ct);
        await app.Names.ReconcileAsync(Ct);
        Assert.IsTrue(await app.Names.SetNameAsync(next.SourcePath, next.MediaType, "User replacement", Ct));
        Assert.AreEqual(new CaptureName("User replacement", false), (await app.Catalog.GetAsync(next.Id, Ct))!.Name);
    }

    [TestMethod]
    public async Task DisableAndReenableDoNotRevivePendingOrDelayedEnrollment()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new Setup(environment);
        await app.InitializeAsync(Ct);
        await app.Names.SetEnabledAsync(true, Ct);
        var pending = await app.CaptureAsync(Ct);
        Guid? oldEpoch = app.Names.Enrollment;
        await app.Names.SetEnabledAsync(false, Ct);
        await app.TitleAsync(pending, "Too late", Ct);
        await app.Names.SetEnabledAsync(true, Ct);
        var delayed = await app.CaptureAsync(Ct, oldEpoch);
        await app.TitleAsync(delayed, "Delayed intake", Ct);
        await app.Names.ReconcileAsync(Ct);
        Assert.IsNull((await app.Catalog.GetAsync(pending.Id, Ct))!.Name);
        Assert.IsNull((await app.Catalog.GetAsync(delayed.Id, Ct))!.Name);
    }

    [TestMethod]
    public async Task ChangedSourceRevocationAndDeletionBoundaryPreventLateNames()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new Setup(environment);
        await app.InitializeAsync(Ct);
        await app.Names.SetEnabledAsync(true, Ct);
        var asset = await app.CaptureAsync(Ct);
        await app.TitleAsync(asset, "Old source", Ct);
        await File.WriteAllTextAsync(asset.SourcePath, "Changed media", Ct);
        await app.Names.ReconcileAsync(Ct);
        Assert.IsNull((await app.Catalog.GetAsync(asset.Id, Ct))!.Name);
        await app.TitleAsync(asset, "Current source", Ct);
        app.Authorization.Block(revokeConsent: true);
        await app.Names.ReconcileAsync(Ct);
        Assert.IsNull((await app.Catalog.GetAsync(asset.Id, Ct))!.Name);
        await app.GrantAsync(Ct);
        await app.Names.ReconcileAsync(Ct);
        Assert.IsNull((await app.Catalog.GetAsync(asset.Id, Ct))!.Name); // Enrollment belongs to the revoked authorization.
        var later = await app.CaptureAsync(Ct);
        // Even if catalog invalidation was unavailable, the metadata deletion watermark fences old candidates.
        await app.Store.ClearAsync(await app.Catalog.GetBoundaryAsync(Ct), Ct);
        await app.TitleAsync(later, "A rerun after deletion", Ct);
        await app.Names.ReconcileAsync(Ct);
        Assert.IsNull((await app.Catalog.GetAsync(later.Id, Ct))!.Name);
    }

    [TestMethod]
    public async Task CommittedTitleNotifiesNamingWithoutProgressOrAnEditor()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new Setup(environment);
        await app.InitializeAsync(Ct);
        await app.Names.SetEnabledAsync(true, Ct);
        var asset = await app.CaptureAsync(Ct);
        await app.TitleAsync(asset, "Committed title", Ct);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Names.Changed += () => changed.TrySetResult();
        app.Worker.Commit(asset.Id, AnalysisCapability.CaptureSynopsis);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.AreEqual("Committed title", (await app.Catalog.GetAsync(asset.Id, Ct))!.Name!.Text);
        var moved = await app.CaptureAsync(Ct);
        string original = moved.SourcePath;
        await app.Catalog.RelocateSourceAsync(moved.Id, original + ".moved", Ct);
        Assert.IsFalse(await app.Catalog.TryApplyAutomaticNameAsync(moved.Id, "Stale location", app.Names.Enrollment!.Value, original, Ct));
    }

    [TestMethod]
    public async Task RestartRecoversAnEligibleCommittedTitleWithoutAnOpenEditor()
    {
        using var environment = new AnalysisTestEnvironment();
        CaptureAsset asset;
        await using (var app = new Setup(environment))
        {
            await app.InitializeAsync(Ct);
            await app.Names.SetEnabledAsync(true, Ct);
            asset = await app.CaptureAsync(Ct);
            await app.TitleAsync(asset, "Recovered title", Ct);
        }
        await using var restarted = new Setup(environment);
        await restarted.Authorization.InitializeAsync(Ct);
        await restarted.Names.InitializeAsync(Ct);
        await restarted.Names.ReconcileAsync(Ct);
        Assert.AreEqual("Recovered title", (await restarted.Catalog.GetAsync(asset.Id, Ct))!.Name!.Text);
    }

    [TestMethod]
    public async Task CatalogSerializesUserOverrideAgainstAnAutomaticWriteAlreadyInFlight()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new Setup(environment);
        await app.InitializeAsync(Ct);
        await app.Names.SetEnabledAsync(true, Ct);
        var asset = await app.CaptureAsync(Ct);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Files.BeforeWrite = async (path, ct) =>
        {
            if (!path.EndsWith("catalog.bin", StringComparison.Ordinal)) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
        };
        var automatic = app.Catalog.TryApplyAutomaticNameAsync(asset.Id, "Automatic", app.Names.Enrollment!.Value, asset.SourcePath, Ct);
        await entered.Task.WaitAsync(Ct);
        var manual = app.Names.SetNameAsync(asset.SourcePath, asset.MediaType, "User choice", Ct);
        release.TrySetResult();
        await automatic;
        Assert.IsTrue(await manual);
        Assert.AreEqual(new CaptureName("User choice", false), (await app.Catalog.GetAsync(asset.Id, Ct))!.Name);
    }

    [TestMethod]
    public async Task RevocationCancelsAnAutomaticNameAwaitingProtectedPublication()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new Setup(environment);
        await app.InitializeAsync(Ct);
        await app.Names.SetEnabledAsync(true, Ct);
        var asset = await app.CaptureAsync(Ct);
        await app.TitleAsync(asset, "Late suggestion", Ct);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Files.BeforeWrite = async (path, ct) =>
        {
            if (!path.EndsWith("catalog.bin", StringComparison.Ordinal)) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
        };
        var applying = app.Names.ReconcileAsync(Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        app.Authorization.Block(revokeConsent: true);
        release.TrySetResult();
        await applying;
        Assert.IsNull((await app.Catalog.GetAsync(asset.Id, Ct))!.Name);
    }

    [TestMethod]
    public async Task ManualNameNeedsNoConsentAndFailedProtectionPreservesThePriorName()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new Setup(environment);
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "import.wav");
        await File.WriteAllTextAsync(path, "media", Ct);
        Assert.IsTrue(await app.Names.SetNameAsync(path, CaptureFileType.Audio, "Personal note", Ct));
        environment.Protector.FailProtection = true;
        Assert.IsFalse(await app.Names.SetNameAsync(path, CaptureFileType.Audio, "Unsaved name", Ct));
        Assert.AreEqual("Personal note", (await app.Names.GetNameAsync(path, Ct))!.Text);
        Assert.IsFalse(await app.Names.SetEnabledAsync(true, Ct));
    }

    [TestMethod]
    [DataRow("CON", "_CON")]
    [DataRow("aux.txt", "_aux.txt")]
    [DataRow("LPT¹", "_LPT¹")]
    [DataRow("Roadmap: résumé / 東京?", "Roadmap_ résumé _ 東京_")]
    [DataRow("...", "Capture")]
    public void SuggestedNamesAreSafeWithoutDiscardingUnicode(string text, string expected) =>
        Assert.AreEqual(expected, new CaptureName(text, false).SuggestedFileName());

    [TestMethod]
    public void NamesRejectMultilineAndOverlongInputAndNeverSplitASurrogate()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureName("  ", false));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureName("two\nlines", false));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureName(new string('a', 161), false));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureName("\ud800", false));
        string suggestion = new CaptureName(new string('a', 119) + "📷", true).SuggestedFileName();
        Assert.AreEqual(119, suggestion.Length);
    }

    private sealed class TestNotifier : IRecentCapturesChangeNotifier
    {
        public event EventHandler? RecentCapturesChanged;
        public void NotifyRecentCapturesChanged() => RecentCapturesChanged?.Invoke(this, EventArgs.Empty);
    }
    private sealed class TestWorker : ICaptureAnalysisWorker
    {
        public AnalysisActivitySnapshot Progress => new(AnalysisActivity.Idle);
        public event Action<AnalysisActivitySnapshot>? ProgressChanged { add { } remove { } }
        public event Action<CaptureId, AnalysisCapability>? ResultCommitted;
        public void Commit(CaptureId id, AnalysisCapability capability) => ResultCommitted?.Invoke(id, capability);
        public Task<bool> EnqueueAsync(AnalysisRequest request, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CancelAsync(CaptureId captureId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AnalysisCleanupResult> ClearAsync(long reconciliationBoundary, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Setup : IAsyncDisposable
    {
        public LocalCaptureAssetCatalog Catalog { get; }
        public LocalCaptureAnalysisStore Store { get; }
        public CaptureMemoryAuthorization Authorization { get; }
        public CaptureNamingService Names { get; }
        public TestWorker Worker { get; } = new();
        private readonly AnalysisTestEnvironment _environment;
        public Setup(AnalysisTestEnvironment environment)
        {
            _environment = environment;
            Catalog = environment.CreateCatalog(); Store = environment.CreateStore();
            Authorization = new(new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files));
            Names = new(Catalog, Catalog, Store, Store, new LocalAnalysisSource(), Authorization,
                Worker, new TestNotifier(), new LocalFileSystem());
        }
        public async Task InitializeAsync(CancellationToken ct)
        {
            await Authorization.InitializeAsync(ct); await GrantAsync(ct); await Names.InitializeAsync(ct);
        }
        public Task GrantAsync(CancellationToken ct) => Authorization.SaveAsync(new(true, true, Guid.NewGuid(), 0), ct);
        public async Task<CaptureAsset> CaptureAsync(CancellationToken ct, Guid? epoch = null)
        {
            Directory.CreateDirectory(_environment.Root);
            string path = Path.Combine(_environment.Root, Guid.NewGuid() + ".png");
            await File.WriteAllTextAsync(path, "media", ct);
            var asset = new CaptureAsset(CaptureId.New(), CaptureFileType.Image, DateTimeOffset.UtcNow, path, CaptureSourceOwnership.Application);
            await Catalog.RegisterForAnalysisAsync(asset, Authorization.Policy.Revision, ct, epoch ?? Names.Enrollment);
            return asset;
        }
        public async Task TitleAsync(CaptureAsset asset, string title, CancellationToken ct)
        {
            await using var source = await new LocalAnalysisSource().OpenAsync(asset.SourcePath, ct);
            var token = await Store.BeginRunAsync(asset.Id, AnalysisMediaKind.Image, source.Revision, "plan", ct);
            var observed = new AnalysisResult(new TextRecognitionMetadata([new("Invoice source")]), AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "plan");
            Assert.IsTrue(await Store.TryWriteAsync(token, observed, ct));
            var synopsis = new CaptureSynopsisMetadata(new(title, [new(observed.ResultId, 0, 0, 14)]), [], new(1, 1, 14, MetadataProcessingLimit.None));
            var result = new AnalysisResult(synopsis, AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "plan",
                derivation: new([new(AnalysisCapability.TextRecognition, observed.ResultId)]));
            Assert.IsTrue(await Store.TryWriteAsync(token, result, ct));
        }
        public async ValueTask DisposeAsync()
        {
            await Names.StopAsync(); Names.Dispose(); Authorization.Dispose(); Catalog.Dispose(); Store.Dispose();
        }
    }
}
