using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Presentation.Features.AnalyzedContent;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class AnalyzedContentViewModelTests
{
    [TestMethod]
    public async Task Load_WithTranscriptMatch_SelectsEvidenceAndSupportsSeeking()
    {
        CaptureId captureId = CaptureId.New();
        var transcript = new SpeechTranscriptV1(
            "First phrase. Matching phrase.",
            [
                new SpeechTranscriptSegmentV1(
                    "First phrase.",
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2)),
                new SpeechTranscriptSegmentV1(
                    "Matching phrase.",
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5)),
            ],
            "en-US");
        var snapshot = new CaptureMetadataViewSnapshot(
            captureId,
            CaptureMediaKind.Audio,
            1,
            null,
            null,
            null,
            transcript,
            null,
            null);
        var viewModel = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));
        TimeSpan? requestedSeek = null;
        viewModel.SeekRequested += (_, position) => requestedSeek = position;

        viewModel.Load(
            new CaptureMetadataViewRequest(CaptureMediaKind.Audio, captureId),
            new CaptureMemoryMatchEvidence(
                CaptureMemoryMatchKind.SpeechTranscript,
                "Matching phrase",
                timecode: TimeSpan.FromSeconds(2)));
        await viewModel.RefreshCompletion;

        Assert.IsTrue(viewModel.IsPaneOpen);
        Assert.AreEqual(AnalyzedContentSectionKind.Transcript, viewModel.SelectedSection.Kind);
        Assert.HasCount(2, viewModel.SelectedSection.Items);
        Assert.IsTrue(viewModel.SelectedSection.Items[1].IsSelected);

        viewModel.SelectedSection.Items[1].ActivateCommand.Execute(null);
        Assert.AreEqual(TimeSpan.FromSeconds(2), requestedSeek);

        viewModel.UpdatePlaybackPosition(TimeSpan.FromSeconds(3));
        Assert.IsFalse(viewModel.SelectedSection.Items[0].IsActive);
        Assert.IsTrue(viewModel.SelectedSection.Items[1].IsActive);

        viewModel.SetSeekRange(TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Assert.IsFalse(viewModel.SelectedSection.Items[1].CanActivate);
    }

    [TestMethod]
    public async Task ImageTextTab_ControlsOverlayVisibilityWithoutRunningAnAnalyzer()
    {
        CaptureId captureId = CaptureId.New();
        var snapshot = new CaptureMetadataViewSnapshot(
            captureId,
            CaptureMediaKind.Image,
            1,
            null,
            new OcrDocumentV1(new PixelSize(100, 100), "hello world", [], []),
            new ImageDescriptionV1("A sample image.", ImageDescriptionPurpose.Brief),
            null,
            null,
            null);
        var viewModel = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));
        List<bool> requests = [];
        viewModel.ImageTextVisibilityRequested += (_, isVisible) => requests.Add(isVisible);

        viewModel.Load(new CaptureMetadataViewRequest(CaptureMediaKind.Image, captureId));
        await viewModel.RefreshCompletion;
        viewModel.IsPaneOpen = true;

        Assert.AreEqual(AnalyzedContentSectionKind.All, viewModel.SelectedSection.Kind);
        viewModel.SelectedSection = viewModel.Sections.Single(section => section.Kind == AnalyzedContentSectionKind.ImageText);
        Assert.AreEqual(AnalyzedContentSectionKind.ImageText, viewModel.SelectedSection.Kind);
        Assert.IsTrue(requests[^1]);

        viewModel.SelectedSection = viewModel.Sections.Single(
            section => section.Kind == AnalyzedContentSectionKind.ImageDescription);
        Assert.IsFalse(requests[^1]);
    }

    [TestMethod]
    [DataRow(CaptureMediaKind.Audio)]
    [DataRow(CaptureMediaKind.Video)]
    public async Task SearchQuery_FiltersSpeechTranscriptRowsIgnoringCase(
        CaptureMediaKind mediaKind)
    {
        CaptureId captureId = CaptureId.New();
        var snapshot = new CaptureMetadataViewSnapshot(
            captureId,
            mediaKind,
            1,
            null,
            null,
            null,
            new SpeechTranscriptV1(
                "Opening remarks. Important conclusion.",
                [
                    new SpeechTranscriptSegmentV1(
                        "Opening remarks.",
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(2)),
                    new SpeechTranscriptSegmentV1(
                        "Important conclusion.",
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(4)),
                ]),
            null,
            null);
        var viewModel = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));

        viewModel.Load(new CaptureMetadataViewRequest(mediaKind, captureId));
        await viewModel.RefreshCompletion;
        viewModel.SearchQuery = " CONCLUSION ";

        Assert.IsTrue(viewModel.IsSearchAvailable);
        Assert.HasCount(1, viewModel.FilteredItems);
        Assert.AreEqual("Important conclusion.", viewModel.FilteredItems[0].Text);

        viewModel.SearchQuery = "not present";

        Assert.IsFalse(viewModel.HasSelectedItems);
        Assert.IsTrue(viewModel.ShowSelectedEmpty);
        Assert.AreEqual("No matching results.", viewModel.SelectedEmptyMessage);
    }

    [TestMethod]
    public async Task SearchQuery_PersistsAcrossSourcesAndCanSearchDescriptions()
    {
        CaptureId captureId = CaptureId.New();
        var snapshot = new CaptureMetadataViewSnapshot(
            captureId,
            CaptureMediaKind.Image,
            1,
            null,
            new OcrDocumentV1(
                new PixelSize(100, 100),
                "Project Alpha\nProject Beta",
                [],
                [
                    new OcrRegionV1(
                        new PixelRect(0, 0, 100, 40),
                        [
                            new OcrLineV1("Project Alpha", new PixelRect(0, 0, 80, 15), []),
                            new OcrLineV1("Project Beta", new PixelRect(0, 20, 80, 15), []),
                        ]),
                ]),
            new ImageDescriptionV1("A project planning board.", ImageDescriptionPurpose.Brief),
            null,
            null,
            null);
        var viewModel = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));

        viewModel.Load(new CaptureMetadataViewRequest(CaptureMediaKind.Image, captureId));
        await viewModel.RefreshCompletion;
        viewModel.SearchQuery = "beta";

        Assert.HasCount(1, viewModel.FilteredItems);
        Assert.AreEqual("Project Beta", viewModel.FilteredItems[0].Text);

        viewModel.SelectedSection = viewModel.Sections.Single(
            section => section.Kind == AnalyzedContentSectionKind.ImageDescription);

        Assert.IsTrue(viewModel.IsSearchAvailable);
        Assert.AreEqual("beta", viewModel.SearchQuery);
        Assert.IsTrue(viewModel.ShowSelectedEmpty);
        viewModel.SearchQuery = "planning";
        Assert.AreEqual("A project planning board.", viewModel.FilteredItems.Single().Text);
        Assert.IsNotEmpty(viewModel.FilteredItems.Single().Highlights);
    }

    [TestMethod]
    public async Task SearchQuery_FiltersVideoOcrRows()
    {
        CaptureId captureId = CaptureId.New();
        var snapshot = new CaptureMetadataViewSnapshot(
            captureId,
            CaptureMediaKind.Video,
            1,
            null,
            null,
            null,
            null,
            new VideoOcrTrackV1(
                "Quarterly results\nRevenue increased",
                [
                    new VideoOcrObservationV1(
                        "Quarterly results",
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(2)),
                    new VideoOcrObservationV1(
                        "Revenue increased",
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(4)),
                ]),
            null);
        var viewModel = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));

        viewModel.Load(new CaptureMetadataViewRequest(CaptureMediaKind.Video, captureId));
        await viewModel.RefreshCompletion;
        viewModel.SearchQuery = "revenue";

        Assert.IsTrue(viewModel.IsSearchAvailable);
        Assert.HasCount(1, viewModel.FilteredItems);
        Assert.AreEqual("Revenue increased", viewModel.FilteredItems[0].Text);
    }

    [TestMethod]
    public async Task Load_OnlyAddsTabsForMetadataWithDisplayableResults()
    {
        CaptureId captureId = CaptureId.New();
        var snapshot = new CaptureMetadataViewSnapshot(
            captureId,
            CaptureMediaKind.Image,
            1,
            new MediaPropertiesV1(CaptureMediaKind.Image),
            new OcrDocumentV1(new PixelSize(100, 100), string.Empty, [], []),
            new ImageDescriptionV1(
                "A lighthouse beside the ocean.",
                ImageDescriptionPurpose.Brief),
            null,
            null,
            null);
        var viewModel = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));

        viewModel.Load(new CaptureMetadataViewRequest(CaptureMediaKind.Image, captureId));
        await viewModel.RefreshCompletion;

        Assert.HasCount(1, viewModel.Sections);
        Assert.AreEqual(AnalyzedContentSectionKind.ImageDescription, viewModel.Sections[0].Kind);
        Assert.AreEqual("AI description", viewModel.Sections[0].Title);
    }

    [TestMethod]
    public async Task AnalysisChangeForOpenCapture_ShouldRefreshCanonicalContent()
    {
        CaptureId captureId = CaptureId.New();
        var service = new StubMetadataViewService(CreateAudioSnapshot(captureId, "First transcript"));
        var notifier = new StubChangeNotifier();
        var viewModel = new AnalyzedContentViewModel(service, notifier);
        viewModel.Load(new CaptureMetadataViewRequest(CaptureMediaKind.Audio, captureId));
        await viewModel.RefreshCompletion;

        service.Snapshot = CreateAudioSnapshot(captureId, "Updated transcript");
        notifier.Raise(captureId);
        await viewModel.RefreshCompletion;

        Assert.AreEqual("Updated transcript", viewModel.Sections[0].FullText);
    }

    [TestMethod]
    public async Task SearchContext_SelectsExactEvidence_AndKeepsSelectionSeparateFromPlayback()
    {
        CaptureId id = CaptureId.New();
        var snapshot = CreateAudioSnapshot(id, "Budget first. Budget second.") with
        {
            Passages = [new(CaptureMemoryMatchKind.SpeechTranscript, "speech:0", "Budget first.", TimeSpan.Zero, TimeSpan.FromSeconds(2)),
                new(CaptureMemoryMatchKind.SpeechTranscript, "speech:1", "Budget second.", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12))],
        };
        using var vm = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));
        var seeks = new List<TimeSpan>();
        vm.SeekRequested += (_, value) => seeks.Add(value);
        vm.Load(new(CaptureMediaKind.Audio, id), new(CaptureMemoryMatchKind.SpeechTranscript, "Budget second.",
            timecode: TimeSpan.FromSeconds(10), evidenceId: "speech:1"), new("budg", 1));
        await vm.RefreshCompletion;
        Assert.IsTrue(vm.IsPaneOpen);
        Assert.IsTrue(vm.IsMatchesView);
        Assert.IsFalse(vm.FollowPlayback);
        Assert.AreEqual("speech:1", vm.SelectedItem?.EvidenceId);
        Assert.AreEqual(2, vm.MatchCount);
        Assert.AreEqual(TimeSpan.FromSeconds(10), seeks.Single());
        vm.UpdatePlaybackPosition(TimeSpan.FromSeconds(1));
        Assert.IsTrue(vm.FilteredItems[0].IsActive);
        Assert.IsFalse(vm.FilteredItems[0].IsSelected);
        Assert.IsTrue(vm.FilteredItems[1].IsSelected);
        Assert.IsFalse(vm.NextMatchCommand.CanExecute(null));
        vm.PreviousMatchCommand.Execute(null);
        Assert.AreEqual("speech:0", vm.SelectedItem?.EvidenceId);
        Assert.AreEqual("Match 1 of 2", vm.MatchCountLabel);
        vm.ShowAllContentCommand.Execute(null);
        Assert.AreEqual("budg", vm.SearchQuery);
        Assert.AreEqual("Copy transcript", vm.CopyActionLabel);
    }

    [TestMethod]
    public async Task MatchOutsideTrim_IsSelectedAndReadable_WithoutSeeking()
    {
        CaptureId id = CaptureId.New();
        using var vm = new AnalyzedContentViewModel(new StubMetadataViewService(CreateAudioSnapshot(id, "Budget")));
        int seeks = 0;
        vm.SeekRequested += (_, _) => seeks++;
        vm.SetSeekRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        vm.Load(new(CaptureMediaKind.Audio, id), new(CaptureMemoryMatchKind.SpeechTranscript, "Budget", timecode: TimeSpan.Zero), new("budget"));
        await vm.RefreshCompletion;
        Assert.AreEqual(0, seeks);
        Assert.IsNotNull(vm.SelectedItem);
        Assert.IsFalse(vm.SelectedItem.CanActivate);
        StringAssert.Contains(vm.SelectedItem.LocationMessage, "playback range");
    }

    [TestMethod]
    public async Task StaleEvidence_DoesNotSeekToAnUnrelatedNewPassage()
    {
        CaptureId id = CaptureId.New();
        var snapshot = CreateAudioSnapshot(id, "Budget") with
        { Passages = [new(CaptureMemoryMatchKind.SpeechTranscript, "new:0", "Budget", TimeSpan.Zero, TimeSpan.FromSeconds(1))] };
        using var vm = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));
        int seeks = 0;
        vm.SeekRequested += (_, _) => seeks++;
        vm.Load(new(CaptureMediaKind.Audio, id), new(CaptureMemoryMatchKind.SpeechTranscript, "Budget",
            timecode: TimeSpan.Zero, evidenceId: "old:0"), new("budget"));
        await vm.RefreshCompletion;
        Assert.AreEqual(0, seeks);
        Assert.IsNull(vm.SelectedItem);
        Assert.HasCount(1, vm.FilteredItems);
        Assert.IsNotEmpty(vm.NavigationMessage);
    }

    [TestMethod]
    public async Task SourceChanged_PreservesTextButDisablesNavigation()
    {
        CaptureId id = CaptureId.New();
        using var vm = new AnalyzedContentViewModel(new StubMetadataViewService(
            CreateAudioSnapshot(id, "Budget") with { IsLocationCurrent = false }));
        vm.Load(new(CaptureMediaKind.Audio, id), searchContext: new("budget"));
        await vm.RefreshCompletion;
        Assert.AreEqual("Budget", vm.FilteredItems.Single().Text);
        Assert.IsFalse(vm.FilteredItems.Single().CanActivate);
        Assert.IsTrue(vm.CopyCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task PendingUnsupportedAndReadyEmpty_AreAvailableAsDistinctSources()
    {
        CaptureId id = CaptureId.New();
        var snapshot = new CaptureMetadataViewSnapshot(id, CaptureMediaKind.Video, 0, null, null, null, null, null, null)
        {
            CapabilityStates = [new(CaptureMemoryMatchKind.SpeechTranscript, CaptureMetadataProcessingState.Queued),
                new(CaptureMemoryMatchKind.VideoOcrText, CaptureMetadataProcessingState.Ready),
                new(CaptureMemoryMatchKind.VideoDescription, CaptureMetadataProcessingState.Unsupported)],
        };
        using var vm = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));
        vm.Load(new(CaptureMediaKind.Video, id));
        await vm.RefreshCompletion;
        Assert.HasCount(3, vm.Sections);
        Assert.IsTrue(vm.HasPendingAnalysis);
        vm.SelectedSection = vm.Sections.Single(s => s.Kind == AnalyzedContentSectionKind.Transcript);
        Assert.AreEqual("Queued for analysis.", vm.SelectedEmptyMessage);
        vm.SelectedSection = vm.Sections.Single(s => s.Kind == AnalyzedContentSectionKind.VideoText);
        Assert.AreEqual("No text was detected.", vm.SelectedEmptyMessage);
        vm.SelectedSection = vm.Sections.Single(s => s.Kind == AnalyzedContentSectionKind.VideoDescription);
        StringAssert.Contains(vm.SelectedEmptyMessage, "unavailable");
    }

    [TestMethod]
    public async Task CombinedSearch_CanBeReadWithoutInventedTiming_AndQueryPersistsInAllContent()
    {
        CaptureId id = CaptureId.New();
        var snapshot = CreateAudioSnapshot(id, "Alpha. Beta.") with
        { Passages = [new(CaptureMemoryMatchKind.SpeechTranscript, "speech:0", "Alpha.", TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            new(CaptureMemoryMatchKind.SpeechTranscript, "speech:1", "Beta.", TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(21))] };
        using var vm = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot));
        vm.Load(new(CaptureMediaKind.Audio, id), new(CaptureMemoryMatchKind.SpeechTranscript, "Alpha. Beta.",
            evidenceId: "speech:-1", isCombinedMatch: true), new("alpha beta"));
        await vm.RefreshCompletion;
        Assert.AreEqual(1, vm.MatchCount);
        Assert.IsNotNull(vm.SelectedItem);
        Assert.IsFalse(vm.SelectedItem.CanActivate);
        Assert.IsNull(vm.SelectedItem.StartTime);
        vm.ShowAllContentCommand.Execute(null);
        Assert.HasCount(2, vm.FilteredItems);
        Assert.IsTrue(vm.FilteredItems.All(item => item.Highlights.Count == 1));
        Assert.AreEqual("alpha beta", vm.SearchQuery);
    }

    [TestMethod]
    public async Task BackToResults_UsesGuardedNavigation_AndRetainsContextWhenCancelled()
    {
        CaptureId id = CaptureId.New();
        var navigation = new Mock<INavigationCoordinator>(MockBehavior.Strict);
        navigation.Setup(service => service.NavigateAsync(NavigationRoute.Home, null, false, default))
            .ReturnsAsync(false);
        using var vm = new AnalyzedContentViewModel(new StubMetadataViewService(CreateAudioSnapshot(id, "Budget")),
            navigation: navigation.Object);
        Assert.IsFalse(vm.BackToResultsCommand.CanExecute(null));
        vm.Load(new(CaptureMediaKind.Audio, id), searchContext: new("budget"));
        await vm.RefreshCompletion;

        await vm.BackToResultsCommand.ExecuteAsync(null);

        navigation.Verify(service => service.NavigateAsync(NavigationRoute.Home, null, false, default), Times.Once);
        Assert.IsTrue(vm.HasSearchContext);
        Assert.AreEqual("budget", vm.SearchQuery);
    }

    [TestMethod]
    public async Task UnchangedMetadataRefresh_PreservesPassageInstancesAndSelection()
    {
        CaptureId id = CaptureId.New();
        var snapshot = CreateAudioSnapshot(id, "Budget") with
        { Passages = [new(CaptureMemoryMatchKind.SpeechTranscript, "speech:0", "Budget", TimeSpan.Zero, TimeSpan.FromSeconds(1))] };
        var changes = new StubChangeNotifier();
        using var vm = new AnalyzedContentViewModel(new StubMetadataViewService(snapshot), changeNotifier: changes);
        vm.Load(new(CaptureMediaKind.Audio, id), searchContext: new("budget"));
        await vm.RefreshCompletion;
        var passage = vm.FilteredItems.Single();
        passage.ActivateCommand.Execute(null);

        changes.Raise(id);
        await vm.RefreshCompletion;

        Assert.AreSame(passage, vm.FilteredItems.Single());
        Assert.AreSame(passage, vm.SelectedItem);
    }

    private static CaptureMetadataViewSnapshot CreateAudioSnapshot(
        CaptureId captureId,
        string transcriptText)
    {
        return new(
            captureId,
            CaptureMediaKind.Audio,
            1,
            null,
            null,
            null,
            new SpeechTranscriptV1(
                transcriptText,
                [new SpeechTranscriptSegmentV1(transcriptText, TimeSpan.Zero, TimeSpan.FromSeconds(1))]),
            null,
            null);
    }

    private sealed class StubMetadataViewService : ICaptureMetadataViewService
    {
        public StubMetadataViewService(CaptureMetadataViewSnapshot? snapshot)
        {
            Snapshot = snapshot;
        }

        public CaptureMetadataViewSnapshot? Snapshot { get; set; }

        public ValueTask<CaptureMetadataViewSnapshot?> GetAsync(
            CaptureMetadataViewRequest request,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Snapshot);
        }
    }

    private sealed class StubChangeNotifier : ICaptureAnalysisChangeNotifier
    {
        public event EventHandler<CaptureAnalysisChangedEventArgs>? AnalysisChanged;

        public void Raise(CaptureId captureId)
        {
            AnalysisChanged?.Invoke(
                this,
                new CaptureAnalysisChangedEventArgs(captureId, wasDeleted: false));
        }
    }

}
