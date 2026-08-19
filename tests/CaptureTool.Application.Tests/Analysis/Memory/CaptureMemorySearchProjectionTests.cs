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
    public async Task PartialTokens_ShouldMatchPrefixesAndInternalUrlFragmentsWithoutShortInfixNoise()
    {
        var fixture = new SearchFixture();
        CaptureId url = fixture.AddCapture(
            13,
            "reference.png",
            "Open https://portal.contoso.com/accounts/settings now",
            description: null);
        using CaptureMemorySearchProjection service = fixture.CreateService();

        IReadOnlyList<CaptureMemorySearchResult> prefix = await service.SearchAsync(
            new CaptureMemorySearchRequest("porta setti", 10));
        IReadOnlyList<CaptureMemorySearchResult> substring = await service.SearchAsync(
            new CaptureMemorySearchRequest("toso count", 10));
        IReadOnlyList<CaptureMemorySearchResult> shortInfix = await service.SearchAsync(
            new CaptureMemorySearchRequest("oso", 10));

        Assert.AreEqual(url, prefix[0].CaptureId);
        Assert.AreEqual(CaptureMemoryMatchKind.OcrText, prefix[0].Evidence.MatchKind);
        Assert.AreEqual(url, substring[0].CaptureId);
        Assert.AreEqual(CaptureMemoryMatchKind.OcrText, substring[0].Evidence.MatchKind);
        Assert.IsGreaterThan(substring[0].Score, prefix[0].Score);
        Assert.IsEmpty(shortInfix);
    }

    [TestMethod]
    public async Task PartialTokenRanking_ShouldPreferExactThenTypoThenPrefixThenSubstring()
    {
        var fixture = new SearchFixture();
        CaptureId exact = fixture.AddCapture(14, "exact.png", "micro", description: null);
        CaptureId typo = fixture.AddCapture(15, "typo.png", "micrp", description: null);
        CaptureId prefix = fixture.AddCapture(16, "prefix.png", "microsoft", description: null);
        CaptureId substring = fixture.AddCapture(
            17,
            "substring.png",
            "supermicroservice",
            description: null);
        using CaptureMemorySearchProjection service = fixture.CreateService();

        IReadOnlyList<CaptureMemorySearchResult> results = await service.SearchAsync(
            new CaptureMemorySearchRequest("micro", 10));

        CollectionAssert.AreEqual(
            new[] { exact, typo, prefix, substring },
            results.Select(result => result.CaptureId).ToArray());
        Assert.IsTrue(results.Zip(results.Skip(1)).All(pair => pair.First.Score > pair.Second.Score));
    }

    [TestMethod]
    public async Task AudioTranscript_ShouldBeSearchableWithMediaKindAndTimedEvidence()
    {
        var fixture = new SearchFixture();
        CaptureId audio = fixture.AddAudioCapture(
            18,
            "standup.wav",
            "We should deploy the capture memory update tomorrow.",
            [new SpeechTranscriptSegmentV1(
                "deploy the capture memory update",
                TimeSpan.FromSeconds(12),
                TimeSpan.FromSeconds(15))]);
        using CaptureMemorySearchProjection service = fixture.CreateService();

        IReadOnlyList<CaptureMemorySearchResult> results = await service.SearchAsync(
            new CaptureMemorySearchRequest("ploy memo", 10));
        IReadOnlyList<CaptureMemorySearchResult> phraseResults = await service.SearchAsync(
            new CaptureMemorySearchRequest("deploy the capture memory", 10));
        IReadOnlyList<CaptureMemorySearchResult> noResults = await service.SearchAsync(
            new CaptureMemorySearchRequest("quarterly budget", 10));

        Assert.HasCount(1, results);
        Assert.AreEqual(audio, results[0].CaptureId);
        Assert.AreEqual(CaptureMediaKind.Audio, results[0].MediaKind);
        Assert.AreEqual(CaptureMemoryMatchKind.SpeechTranscript, results[0].Evidence.MatchKind);
        Assert.AreEqual(TimeSpan.FromSeconds(12), results[0].Evidence.Timecode);
        StringAssert.Contains(results[0].Evidence.Snippet, "capture memory");
        Assert.HasCount(1, phraseResults);
        Assert.AreEqual(TimeSpan.FromSeconds(12), phraseResults[0].Evidence.Timecode);
        Assert.IsEmpty(noResults);
    }

    [TestMethod]
    public async Task VideoOcrAndTranscript_ShouldBeSearchableWithDistinctTimedEvidence()
    {
        var fixture = new SearchFixture();
        CaptureId video = fixture.AddVideoCapture(
            19,
            "demo.mp4",
            "Open the Contoso deployment dashboard",
            [new VideoOcrObservationV1(
                "Contoso deployment dashboard",
                TimeSpan.FromSeconds(3.5),
                TimeSpan.FromSeconds(7))],
            "The narrator mentions the emergency rollback procedure.",
            [new SpeechTranscriptSegmentV1(
                "emergency rollback procedure",
                TimeSpan.FromSeconds(11),
                TimeSpan.FromSeconds(14))]);
        using CaptureMemorySearchProjection service = fixture.CreateService();

        IReadOnlyList<CaptureMemorySearchResult> ocr = await service.SearchAsync(
            new CaptureMemorySearchRequest("toso deploy", 10));
        IReadOnlyList<CaptureMemorySearchResult> speech = await service.SearchAsync(
            new CaptureMemorySearchRequest("ergency roll", 10));

        Assert.HasCount(1, ocr);
        Assert.AreEqual(video, ocr[0].CaptureId);
        Assert.AreEqual(CaptureMediaKind.Video, ocr[0].MediaKind);
        Assert.AreEqual(CaptureMemoryMatchKind.VideoOcrText, ocr[0].Evidence.MatchKind);
        Assert.AreEqual(TimeSpan.FromSeconds(3.5), ocr[0].Evidence.Timecode);
        Assert.IsNull(ocr[0].Evidence.PixelBounds);
        Assert.HasCount(1, speech);
        Assert.AreEqual(CaptureMemoryMatchKind.SpeechTranscript, speech[0].Evidence.MatchKind);
        Assert.AreEqual(TimeSpan.FromSeconds(11), speech[0].Evidence.Timecode);
    }

    [TestMethod]
    public async Task VideoDescription_ShouldBeSearchableWithVisualEvidenceAndTimecode()
    {
        var fixture = new SearchFixture();
        CaptureId video = fixture.AddVideoCapture(
            20,
            "walkthrough.mp4",
            "unrelated screen text",
            [new VideoOcrObservationV1(
                "unrelated screen text",
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2))],
            "unrelated narration",
            [new SpeechTranscriptSegmentV1(
                "unrelated narration",
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2))],
            "A person points to the turquoise deployment graph.",
            [new VideoDescriptionObservationV1(
                "A person points to the turquoise deployment graph.",
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(45))]);
        using CaptureMemorySearchProjection service = fixture.CreateService();

        IReadOnlyList<CaptureMemorySearchResult> results = await service.SearchAsync(
            new CaptureMemorySearchRequest("quoise deploy", 10));

        Assert.HasCount(1, results);
        Assert.AreEqual(video, results[0].CaptureId);
        Assert.AreEqual(CaptureMediaKind.Video, results[0].MediaKind);
        Assert.AreEqual(CaptureMemoryMatchKind.VideoDescription,
            results[0].Evidence.MatchKind);
        Assert.AreEqual(TimeSpan.FromSeconds(30), results[0].Evidence.Timecode);
        StringAssert.Contains(results[0].Evidence.Snippet, "turquoise deployment graph");
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
    public async Task WarmExactAndPartialSearchP95_ShouldRemainUnder150MillisecondsForOneThousandImages()
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
        var exactRequest = new CaptureMemorySearchRequest("common benchmark phrase", 50);
        var partialRequest = new CaptureMemorySearchRequest("comm bench phra", 50);
        _ = await service.SearchAsync(exactRequest);
        _ = await service.SearchAsync(partialRequest);

        var exactElapsed = new List<double>();
        var partialElapsed = new List<double>();
        for (int iteration = 0; iteration < 30; iteration++)
        {
            Stopwatch exactStopwatch = Stopwatch.StartNew();
            IReadOnlyList<CaptureMemorySearchResult> exactResults =
                await service.SearchAsync(exactRequest);
            exactStopwatch.Stop();
            Assert.HasCount(50, exactResults);
            exactElapsed.Add(exactStopwatch.Elapsed.TotalMilliseconds);

            Stopwatch partialStopwatch = Stopwatch.StartNew();
            IReadOnlyList<CaptureMemorySearchResult> partialResults =
                await service.SearchAsync(partialRequest);
            partialStopwatch.Stop();
            Assert.HasCount(50, partialResults);
            partialElapsed.Add(partialStopwatch.Elapsed.TotalMilliseconds);
        }

        exactElapsed.Sort();
        partialElapsed.Sort();
        double exactP95 = exactElapsed[(int)Math.Ceiling(exactElapsed.Count * 0.95) - 1];
        double partialP95 = partialElapsed[(int)Math.Ceiling(partialElapsed.Count * 0.95) - 1];
        TestContext.WriteLine(
            $"Capture Memory warm p95 for 1,000 images: exact={exactP95:F3} ms, partial={partialP95:F3} ms");
        Assert.IsLessThan(150, exactP95);
        Assert.IsLessThan(150, partialP95);
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

        public CaptureId AddAudioCapture(
            int identity,
            string retainedFilename,
            string transcript,
            IReadOnlyList<SpeechTranscriptSegmentV1> segments)
        {
            CaptureId captureId = CaptureIdFor(identity);
            DateTimeOffset capturedAt = AnalysisTestData.CapturedAtUtc;
            SourceRevision sourceRevision = new(
                100,
                capturedAt,
                ContentFingerprint.Sha256(new string('b', 64)));
            var payload = new SpeechTranscriptV1(transcript, segments, "en-US");
            var analysis = new CapabilityAnalysis(
                AnalysisCapabilities.SpeechTranscriptV1,
                new CanonicalCapabilityResult(
                    captureId,
                    sourceRevision,
                    payload,
                    AnalysisTestData.CreateAnalyzer(),
                    ProcessingBoundary.OnDevice,
                    capturedAt.AddSeconds(1)),
                latestOutcome: null);
            CaptureAnalysisRecipe recipe = CaptureAnalysisRecipeDefaults.CreateCaptureMemoryAudioRecipe();
            Store.Snapshots[captureId] = new CaptureAnalysisStoreSnapshot(
                1,
                new CaptureAnalysisRecord(
                    captureId,
                    CaptureMediaKind.Audio,
                    capturedAt,
                    sourceRevision,
                    recipe,
                    [analysis]));
            Assets[captureId] = new CaptureAsset(
                captureId,
                CaptureFileType.Audio,
                Path.Combine(@"C:\CaptureTool\Captures", retainedFilename),
                CaptureSourceOwnership.AppOwned,
                capturedAt);
            _control.Enroll(captureId, identity + 1L, recipe);
            return captureId;
        }

        public CaptureId AddVideoCapture(
            int identity,
            string retainedFilename,
            string videoOcrText,
            IReadOnlyList<VideoOcrObservationV1> observations,
            string transcript,
            IReadOnlyList<SpeechTranscriptSegmentV1> transcriptSegments,
            string? visualDescription = null,
            IReadOnlyList<VideoDescriptionObservationV1>? descriptionObservations = null)
        {
            CaptureId captureId = CaptureIdFor(identity);
            DateTimeOffset capturedAt = AnalysisTestData.CapturedAtUtc;
            SourceRevision sourceRevision = new(
                100,
                capturedAt,
                ContentFingerprint.Sha256(new string('c', 64)));
            AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
            var videoOcr = new VideoOcrTrackV1(videoOcrText, observations);
            var speech = new SpeechTranscriptV1(transcript, transcriptSegments, "en-US");
            var analyses = new List<CapabilityAnalysis>
            {
                new CapabilityAnalysis(
                    AnalysisCapabilities.VideoOcrTrackV1,
                    new CanonicalCapabilityResult(
                        captureId,
                        sourceRevision,
                        videoOcr,
                        analyzer,
                        ProcessingBoundary.OnDevice,
                        capturedAt.AddSeconds(1)),
                    latestOutcome: null),
                new CapabilityAnalysis(
                    AnalysisCapabilities.SpeechTranscriptV1,
                    new CanonicalCapabilityResult(
                        captureId,
                        sourceRevision,
                        speech,
                        analyzer,
                        ProcessingBoundary.OnDevice,
                        capturedAt.AddSeconds(2)),
                    latestOutcome: null),
            };
            if (visualDescription != null)
            {
                var descriptions = new VideoDescriptionTrackV1(
                    visualDescription,
                    descriptionObservations);
                analyses.Add(new CapabilityAnalysis(
                    AnalysisCapabilities.VideoDescriptionTrackV1,
                    new CanonicalCapabilityResult(
                        captureId,
                        sourceRevision,
                        descriptions,
                        analyzer,
                        ProcessingBoundary.OnDevice,
                        capturedAt.AddSeconds(3)),
                    latestOutcome: null));
            }
            CaptureAnalysisRecipe recipe =
                CaptureAnalysisRecipeDefaults.CreateCaptureMemoryVideoRecipe();
            Store.Snapshots[captureId] = new CaptureAnalysisStoreSnapshot(
                1,
                new CaptureAnalysisRecord(
                    captureId,
                    CaptureMediaKind.Video,
                    capturedAt,
                    sourceRevision,
                    recipe,
                    analyses));
            Assets[captureId] = new CaptureAsset(
                captureId,
                CaptureFileType.Video,
                Path.Combine(@"C:\CaptureTool\Captures", retainedFilename),
                CaptureSourceOwnership.AppOwned,
                capturedAt);
            _control.Enroll(captureId, identity + 1L, recipe);
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

        public void Enroll(
            CaptureId captureId,
            long finalizationSequence,
            CaptureAnalysisRecipe? selectedRecipe = null)
        {
            CaptureAnalysisRecipe recipe = selectedRecipe ?? CaptureAnalysisRecipeDefaults
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
