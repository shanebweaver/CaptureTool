using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Presentation.Features.AnalyzedContent;

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

        Assert.AreEqual(AnalyzedContentSectionKind.ImageText, viewModel.SelectedSection.Kind);
        Assert.IsTrue(requests[^1]);

        viewModel.SelectedSection = viewModel.Sections.Single(
            section => section.Kind == AnalyzedContentSectionKind.ImageDescription);
        Assert.IsFalse(requests[^1]);
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
        Assert.AreEqual("Image description", viewModel.Sections[0].Title);
    }

    [TestMethod]
    public async Task ReanalyzeCommands_ScheduleTheSelectedCapabilityOrTheFullRecipe()
    {
        CaptureId captureId = CaptureId.New();
        var snapshot = new CaptureMetadataViewSnapshot(
            captureId,
            CaptureMediaKind.Image,
            1,
            null,
            new OcrDocumentV1(new PixelSize(100, 100), "hello world", [], []),
            null,
            null,
            null,
            null);
        var maintenance = new StubMaintenanceService();
        var viewModel = new AnalyzedContentViewModel(
            new StubMetadataViewService(snapshot),
            maintenance: maintenance);
        viewModel.Load(new CaptureMetadataViewRequest(CaptureMediaKind.Image, captureId));
        await viewModel.RefreshCompletion;

        await viewModel.ReanalyzeSelectedCommand.ExecuteAsync(null);
        await viewModel.ReanalyzeAllCommand.ExecuteAsync(null);

        Assert.HasCount(2, maintenance.Requests);
        CollectionAssert.AreEqual(
            new[] { AnalysisCapabilities.OcrDocumentV1.Id },
            maintenance.Requests[0].CapabilityIds.ToArray());
        Assert.IsEmpty(maintenance.Requests[1].CapabilityIds);
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

    private sealed class StubMaintenanceService : ICaptureAnalysisMaintenanceService
    {
        public List<CaptureAnalysisReanalysisRequest> Requests { get; } = [];

        public ValueTask<CaptureAnalysisMaintenanceResult> ClearMemoryAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<CaptureAnalysisMaintenanceResult> RebuildSearchIndexAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<CaptureAnalysisMaintenanceResult> ReanalyzeCapturesAsync(
            CaptureAnalysisReanalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(new CaptureAnalysisMaintenanceResult(
                CaptureAnalysisMaintenanceStatus.Succeeded,
                1));
        }

        public ValueTask<CaptureAnalysisMaintenanceResult> ReanalyzeCapturesAsync(
            CaptureAnalysisReanalysisRequest request,
            IProgress<CaptureAnalysisMaintenanceProgress> progress,
            CancellationToken cancellationToken = default)
            => ReanalyzeCapturesAsync(request, cancellationToken);
    }
}
