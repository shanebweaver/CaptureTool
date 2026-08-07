using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Analysis.Memory;
using CaptureTool.Application.Tests.Analysis.Domain;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CaptureTool.Application.Tests.Analysis.Memory;

[TestClass]
public sealed class CaptureMemorySearchProjectionTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task LabeledSyntheticCorpus_ShouldRankExplainableFieldsAndReturnOcrBounds()
    {
        var fixture = new SearchFixture();
        CaptureId filename = fixture.AddCapture(
            1,
            "archive.png",
            "meeting notes",
            "A whiteboard for Project Atlas",
            preferredFilename: "Project-Atlas.png");
        CaptureId ocr = fixture.AddCapture(
            2,
            "settings.png",
            "Open advanced settings now",
            description: null);
        CaptureId description = fixture.AddCapture(
            3,
            "diagram.png",
            "unrelated text",
            "A blue dashboard showing advanced settings");
        _ = fixture.AddCapture(
            4,
            "advanced-reference.png",
            "unrelated text",
            "unrelated image");

        using CaptureMemorySearchProjection service = fixture.CreateService();

        IReadOnlyList<CaptureMemorySearchResult> atlas = await service.SearchAsync(
            new CaptureMemorySearchRequest("project atlas", 10));
        IReadOnlyList<CaptureMemorySearchResult> settings = await service.SearchAsync(
            new CaptureMemorySearchRequest("advanced settings", 10));

        Assert.AreEqual(filename, atlas[0].CaptureId);
        Assert.AreEqual(CaptureMemoryMatchKind.Filename, atlas[0].Evidence.MatchKind);
        Assert.AreEqual("Project-Atlas.png", atlas[0].Evidence.Snippet);
        Assert.IsFalse(atlas[0].Evidence.Snippet.Contains(@"C:\", StringComparison.Ordinal));

        CollectionAssert.AreEqual(
            new[] { ocr, description },
            settings.Take(2).Select(result => result.CaptureId).ToArray());
        Assert.AreEqual(CaptureMemoryMatchKind.OcrText, settings[0].Evidence.MatchKind);
        CaptureMemoryPixelBounds bounds = settings[0].Evidence.PixelBounds!;
        Assert.IsNotNull(bounds);
        Assert.AreEqual(10, bounds.X);
        Assert.AreEqual(1920, bounds.RasterWidth);
        Assert.IsGreaterThan(settings[1].Score, settings[0].Score);
        Assert.AreEqual(1, settings[0].Rank);
        Assert.AreEqual(2, settings[1].Rank);
    }

    [TestMethod]
    public async Task OcrOnlyUnicodePunctuationAndConservativeTypos_ShouldBeSearchableDeterministically()
    {
        var fixture = new SearchFixture();
        CaptureId unicode = fixture.AddCapture(
            10,
            "unicode.png",
            "Café—Résumé 設定 panel",
            description: null);
        CaptureId typo = fixture.AddCapture(
            11,
            "preferences.png",
            "Application settings panel",
            description: null);
        _ = fixture.AddCapture(12, "animal.png", "cat portrait", description: null);

        using CaptureMemorySearchProjection service = fixture.CreateService();

        IReadOnlyList<CaptureMemorySearchResult> unicodeResults = await service.SearchAsync(
            new CaptureMemorySearchRequest("Cafe\u0301, re\u0301sume\u0301!", 10));
        IReadOnlyList<CaptureMemorySearchResult> typoResults = await service.SearchAsync(
            new CaptureMemorySearchRequest("setings panel", 10));
        IReadOnlyList<CaptureMemorySearchResult> shortTypo = await service.SearchAsync(
            new CaptureMemorySearchRequest("cut", 10));
        IReadOnlyList<CaptureMemorySearchResult> twoTypos = await service.SearchAsync(
            new CaptureMemorySearchRequest("settngs panle", 10));

        Assert.AreEqual(unicode, unicodeResults[0].CaptureId);
        Assert.AreEqual(typo, typoResults[0].CaptureId);
        Assert.IsEmpty(shortTypo);
        Assert.IsEmpty(twoTypos);
    }

    [TestMethod]
    public async Task Rebuild_ShouldExcludeDuplicateStaleMissingDeletedAndTombstonedItems()
    {
        var fixture = new SearchFixture();
        CaptureId firstDuplicate = fixture.AddCapture(20, "one.png", "duplicate needle", null);
        CaptureId secondDuplicate = fixture.AddCapture(
            21,
            "two.png",
            "duplicate needle",
            null,
            capturedMinute: 1);
        CaptureId missing = fixture.AddCapture(22, "missing.png", "duplicate needle", null);
        CaptureId deleted = fixture.AddCapture(23, "deleted.png", "duplicate needle", null);
        CaptureId excluded = fixture.AddCapture(24, "excluded.png", "duplicate needle", null);
        fixture.Assets.Remove(missing);
        fixture.Assets[deleted] = fixture.Assets[deleted].MarkDeleted();
        fixture.Exclude(excluded);

        using CaptureMemorySearchProjection service = fixture.CreateService();

        IReadOnlyList<CaptureMemorySearchResult> results = await service.SearchAsync(
            new CaptureMemorySearchRequest("duplicate needle", 10));
        IReadOnlyList<CaptureMemorySearchResult> noMatch = await service.SearchAsync(
            new CaptureMemorySearchRequest("absent phrase", 10));

        CollectionAssert.AreEqual(
            new[] { secondDuplicate, firstDuplicate },
            results.Select(result => result.CaptureId).ToArray());
        Assert.IsEmpty(noMatch);
    }

    [TestMethod]
    public async Task Ranking_ShouldRemainStableAcrossInputOrderAndRebuilds()
    {
        var fixture = new SearchFixture();
        _ = fixture.AddCapture(30, "first.png", "same deterministic phrase", null, capturedMinute: 0);
        _ = fixture.AddCapture(31, "second.png", "same deterministic phrase", null, capturedMinute: 0);
        _ = fixture.AddCapture(32, "third.png", "same deterministic phrase", null, capturedMinute: 0);

        using CaptureMemorySearchProjection forward = fixture.CreateService();
        IReadOnlyList<CaptureMemorySearchResult> first = await forward.SearchAsync(
            new CaptureMemorySearchRequest("deterministic phrase", 10));

        fixture.Store.ReverseReadOrder = true;
        using CaptureMemorySearchProjection reverse = fixture.CreateService();
        IReadOnlyList<CaptureMemorySearchResult> second = await reverse.SearchAsync(
            new CaptureMemorySearchRequest("deterministic phrase", 10));
        _ = await reverse.RebuildAsync();
        IReadOnlyList<CaptureMemorySearchResult> third = await reverse.SearchAsync(
            new CaptureMemorySearchRequest("deterministic phrase", 10));

        CollectionAssert.AreEqual(
            first.Select(ResultIdentity).ToArray(),
            second.Select(ResultIdentity).ToArray());
        CollectionAssert.AreEqual(
            second.Select(ResultIdentity).ToArray(),
            third.Select(ResultIdentity).ToArray());
    }

    [TestMethod]
    public async Task RemoveClearRestartAndRebuild_ShouldNeverResurrectTombstonedResults()
    {
        var fixture = new SearchFixture();
        CaptureId captureId = fixture.AddCapture(40, "private.png", "secret recovery phrase", null);
        using CaptureMemorySearchProjection service = fixture.CreateService();
        Assert.HasCount(1, await service.SearchAsync(
            new CaptureMemorySearchRequest("secret recovery", 10)));

        await service.RemoveAsync(captureId);
        Assert.IsEmpty(await service.SearchAsync(
            new CaptureMemorySearchRequest("secret recovery", 10)));

        fixture.Exclude(captureId);
        _ = await service.RebuildAsync();
        Assert.IsEmpty(await service.SearchAsync(
            new CaptureMemorySearchRequest("secret recovery", 10)));

        using CaptureMemorySearchProjection restarted = fixture.CreateService();
        Assert.IsEmpty(await restarted.SearchAsync(
            new CaptureMemorySearchRequest("secret recovery", 10)));

        await restarted.ClearAsync();
        Assert.IsEmpty(await restarted.SearchAsync(
            new CaptureMemorySearchRequest("secret recovery", 10)));
        Assert.IsTrue(fixture.Store.Snapshots.ContainsKey(captureId),
            "Projection maintenance must not mutate canonical metadata.");
    }

    [TestMethod]
    public async Task Refresh_ShouldUsePreferredOpenFilenameThenFallBackToRetainedSourceFilename()
    {
        var fixture = new SearchFixture();
        CaptureId captureId = fixture.AddCapture(50, "retained-source.png", "ordinary text", null);
        using CaptureMemorySearchProjection service = fixture.CreateService();
        Assert.HasCount(1, await service.SearchAsync(
            new CaptureMemorySearchRequest("retained source", 10)));

        CaptureAsset current = fixture.Assets[captureId];
        fixture.Assets[captureId] = current.ChangePreferredOpenPath(@"C:\Exports\preferred-export.png");
        await service.RefreshAsync(captureId);

        IReadOnlyList<CaptureMemorySearchResult> preferred = await service.SearchAsync(
            new CaptureMemorySearchRequest("preferred export", 10));
        IReadOnlyList<CaptureMemorySearchResult> retained = await service.SearchAsync(
            new CaptureMemorySearchRequest("retained source", 10));

        Assert.AreEqual(captureId, preferred[0].CaptureId);
        Assert.AreEqual("preferred-export.png", preferred[0].Evidence.Snippet);
        Assert.IsEmpty(retained);
    }

    [TestMethod]
    public async Task FailedProjectionRebuild_ShouldLeaveLastGoodProjectionAndCanonicalContentIntact()
    {
        var fixture = new SearchFixture();
        CaptureId captureId = fixture.AddCapture(60, "stable.png", "canonical content remains", null);
        string retainedSource = fixture.Assets[captureId].RetainedSourcePath;
        using CaptureMemorySearchProjection service = fixture.CreateService();
        Assert.HasCount(1, await service.SearchAsync(
            new CaptureMemorySearchRequest("canonical content", 10)));

        fixture.Store.ThrowOnReadAll = true;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            _ = await service.RebuildAsync());

        Assert.HasCount(1, await service.SearchAsync(
            new CaptureMemorySearchRequest("canonical content", 10)));
        Assert.IsTrue(fixture.Store.Snapshots.ContainsKey(captureId));
        Assert.AreEqual(retainedSource, fixture.Assets[captureId].RetainedSourcePath);
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task WarmSearchP95_ShouldRemainUnder150MillisecondsForOneThousandImages()
    {
        var fixture = new SearchFixture();
        for (int index = 1; index <= 1000; index++)
        {
            _ = fixture.AddCapture(
                10_000 + index,
                $"capture-{index:D4}.png",
                $"Window {index:D4} project status common benchmark phrase",
                $"A dashboard for project {index:D4}",
                capturedMinute: index);
        }

        using CaptureMemorySearchProjection service = fixture.CreateService();
        var request = new CaptureMemorySearchRequest("common benchmark phrase", 50);
        _ = await service.SearchAsync(request);

        var elapsed = new List<double>();
        for (int iteration = 0; iteration < 30; iteration++)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            IReadOnlyList<CaptureMemorySearchResult> results = await service.SearchAsync(request);
            stopwatch.Stop();
            Assert.HasCount(50, results);
            elapsed.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        elapsed.Sort();
        double p95 = elapsed[(int)Math.Ceiling(elapsed.Count * 0.95) - 1];
        TestContext.WriteLine($"Capture Memory warm p95 for 1,000 images: {p95:F3} ms");
        Assert.IsLessThan(150, p95);
    }

    private static string ResultIdentity(CaptureMemorySearchResult result)
    {
        return $"{result.Rank}|{result.CaptureId}|{result.Score:R}|{result.Evidence.MatchKind}";
    }

    private sealed class SearchFixture
    {
        private readonly MutableControlStore _control = new();

        public MutableAnalysisStore Store { get; } = new();

        public Dictionary<CaptureId, CaptureAsset> Assets { get; } = [];

        public CaptureId AddCapture(
            int identity,
            string retainedFilename,
            string? ocr,
            string? description,
            string? preferredFilename = null,
            int capturedMinute = 0)
        {
            CaptureId captureId = CaptureIdFor(identity);
            DateTimeOffset capturedAt = AnalysisTestData.CapturedAtUtc.AddMinutes(capturedMinute);
            CaptureAnalysisRecord record = CreateRecord(captureId, capturedAt, ocr, description);
            Store.Snapshots[captureId] = new CaptureAnalysisStoreSnapshot(1, record);
            Assets[captureId] = new CaptureAsset(
                captureId,
                CaptureFileType.Image,
                Path.Combine(@"C:\CaptureTool\Captures", retainedFilename),
                CaptureSourceOwnership.AppOwned,
                capturedAt,
                preferredFilename == null
                    ? null
                    : Path.Combine(@"C:\Users\Person\Pictures", preferredFilename));
            _control.Enroll(captureId, identity + 1L);
            return captureId;
        }

        public void Exclude(CaptureId captureId)
        {
            _control.Exclude(captureId);
        }

        public CaptureMemorySearchProjection CreateService()
        {
            return new CaptureMemorySearchProjection(
                Store,
                _control,
                new MutableAssetCatalog(Assets));
        }
    }

    private sealed class MutableAnalysisStore : ICaptureAnalysisStore
    {
        public Dictionary<CaptureId, CaptureAnalysisStoreSnapshot> Snapshots { get; } = [];

        public bool ReverseReadOrder { get; set; }

        public bool ThrowOnReadAll { get; set; }

        public ValueTask<CaptureAnalysisStoreSnapshot?> GetAsync(
            CaptureId captureId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Snapshots.TryGetValue(captureId, out CaptureAnalysisStoreSnapshot? snapshot);
            return ValueTask.FromResult(snapshot);
        }

        public async IAsyncEnumerable<CaptureAnalysisStoreSnapshot> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            if (ThrowOnReadAll)
            {
                throw new InvalidDataException("Synthetic projection corruption.");
            }

            IEnumerable<CaptureAnalysisStoreSnapshot> ordered = Snapshots.Values
                .OrderBy(snapshot => snapshot.Record.CaptureId.ToString(), StringComparer.Ordinal);
            if (ReverseReadOrder)
            {
                ordered = ordered.Reverse();
            }

            foreach (CaptureAnalysisStoreSnapshot snapshot in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return snapshot;
            }
        }
    }

    private sealed class MutableControlStore : ICaptureAnalysisControlStore
    {
        private readonly Dictionary<CaptureId, CaptureAnalysisEnrollment> _enrollments = [];
        private long _revision = 1;

        public void Enroll(CaptureId captureId, long finalizationSequence)
        {
            CaptureAnalysisRecipe recipe = CaptureAnalysisRecipeDefaults
                .CreateCaptureMemoryImageRecipe();
            _enrollments[captureId] = new CaptureAnalysisEnrollment(
                captureId,
                CaptureAnalysisEnrollmentState.Enrolled,
                CaptureAnalysisExclusionReason.None,
                enrollmentGeneration: 1,
                tombstoneGeneration: 0,
                finalizationSequence,
                recipe.Id,
                recipe.Version);
            _revision++;
        }

        public void Exclude(CaptureId captureId)
        {
            CaptureAnalysisEnrollment current = _enrollments[captureId];
            _enrollments[captureId] = new CaptureAnalysisEnrollment(
                captureId,
                CaptureAnalysisEnrollmentState.Excluded,
                CaptureAnalysisExclusionReason.UserExcluded,
                checked(current.EnrollmentGeneration + 1),
                checked(current.TombstoneGeneration + 1),
                current.AssetFinalizationSequence,
                requestedRecipeId: null,
                requestedRecipeVersion: null);
            _revision++;
        }

        public ValueTask<CaptureAnalysisControlSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureAnalysisPolicy policy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
                CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
                currentSequence: 1);
            return ValueTask.FromResult(new CaptureAnalysisControlSnapshot(
                _revision,
                new CaptureAnalysisControlState(policy, _enrollments.Values)));
        }

        public ValueTask<CaptureAnalysisControlWriteResult> TryWriteAsync(
            CaptureAnalysisControlState state,
            long expectedDocumentRevision,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class MutableAssetCatalog(
        Dictionary<CaptureId, CaptureAsset> assets) : ICaptureAssetCatalog
    {
        public IReadOnlyList<CaptureAsset> GetAssets() => [.. assets.Values];

        public CaptureAsset? Get(CaptureId captureId) => assets.GetValueOrDefault(captureId);

        public CaptureAsset? FindByPath(string filePath) => assets.Values.FirstOrDefault(asset =>
            string.Equals(asset.RetainedSourcePath, filePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(asset.PreferredOpenPath, filePath, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence) => [];

        public long GetLatestChangeSequence() => 0;

        public CaptureAssetCatalogWriteResult TryAdd(CaptureAsset asset) =>
            throw new NotSupportedException();

        public IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(
            IReadOnlyList<CaptureAsset> newAssets) => throw new NotSupportedException();

        public CaptureAssetCatalogWriteResult TryUpdate(
            CaptureAsset asset,
            long expectedLifecycleRevision,
            CaptureAssetChangeType changeType) => throw new NotSupportedException();

        public CaptureAssetCatalogWriteResult TryForget(
            CaptureId captureId,
            long expectedLifecycleRevision) => throw new NotSupportedException();
    }

    private static CaptureAnalysisRecord CreateRecord(
        CaptureId captureId,
        DateTimeOffset capturedAtUtc,
        string? ocrText,
        string? description)
    {
        SourceRevision sourceRevision = new(
            100,
            capturedAtUtc,
            ContentFingerprint.Sha256(new string('a', 64)));
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        var analyses = new List<CapabilityAnalysis>();
        if (ocrText != null)
        {
            var rasterSize = new PixelSize(1920, 1080);
            var bounds = new PixelRect(10, 20, 800, 60);
            var ocr = new OcrDocumentV1(
                rasterSize,
                ocrText,
                [new OcrLanguageCandidateV1("en-US")],
                [new OcrRegionV1(
                    bounds,
                    [new OcrLineV1(ocrText, bounds, [])])]);
            analyses.Add(new CapabilityAnalysis(
                AnalysisCapabilities.OcrDocumentV1,
                new CanonicalCapabilityResult(
                    captureId,
                    sourceRevision,
                    ocr,
                    analyzer,
                    ProcessingBoundary.OnDevice,
                    capturedAtUtc.AddSeconds(1)),
                latestOutcome: null));
        }

        if (description != null)
        {
            var payload = new ImageDescriptionV1(description, ImageDescriptionPurpose.Brief);
            analyses.Add(new CapabilityAnalysis(
                AnalysisCapabilities.ImageDescriptionV1,
                new CanonicalCapabilityResult(
                    captureId,
                    sourceRevision,
                    payload,
                    analyzer,
                    ProcessingBoundary.OnDevice,
                    capturedAtUtc.AddSeconds(2)),
                latestOutcome: null));
        }

        return new CaptureAnalysisRecord(
            captureId,
            CaptureMediaKind.Image,
            capturedAtUtc,
            sourceRevision,
            CaptureAnalysisRecipeDefaults.CreateCaptureMemoryImageRecipe(),
            analyses);
    }

    private static CaptureId CaptureIdFor(int identity)
    {
        return new CaptureId(Guid.Parse($"00000000-0000-0000-0000-{identity:D12}"));
    }
}
