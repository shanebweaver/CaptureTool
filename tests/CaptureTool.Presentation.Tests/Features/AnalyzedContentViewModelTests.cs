using CaptureTool.Application.Abstractions.Analysis.Memory;
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
    public void CurrentImageResults_ReplaceCanonicalSectionsAndCanBeInvalidated()
    {
        var viewModel = new AnalyzedContentViewModel();

        viewModel.SetCurrentImageText(
            "hello world",
            [("hello world", new PixelRect(1, 2, 30, 10))]);
        viewModel.SetCurrentImageDescription("A sample image.");

        Assert.HasCount(2, viewModel.Sections);
        Assert.AreEqual("hello world", viewModel.Sections[0].FullText);
        Assert.AreEqual("A sample image.", viewModel.Sections[1].FullText);

        viewModel.ClearImageDerivedContent();

        Assert.IsFalse(viewModel.HasContent);
        Assert.IsEmpty(viewModel.Sections);
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
}
