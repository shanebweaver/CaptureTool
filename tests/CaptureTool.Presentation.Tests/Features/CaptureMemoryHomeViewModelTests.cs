using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.Home;
using CaptureTool.Presentation.Features.RecentCaptures;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureMemoryHomeViewModelTests
{
    [TestMethod]
    public async Task EmptyQuery_PreservesRecentGalleryAndDoesNotSearch()
    {
        var search = new Mock<ICaptureMemorySearchService>(MockBehavior.Strict);
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(search.Object);

        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SearchQuery = "   ";
        await viewModel.SearchCompletion;

        Assert.IsTrue(viewModel.ShowRecentGallery);
        Assert.IsFalse(viewModel.ShowResults);
        search.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task IndexChanges_ShouldRefreshTheCurrentQueryAfterEraseAndReanalysis()
    {
        CaptureId id = CaptureId.New();
        IReadOnlyList<CaptureMemorySearchResult> matches = [];
        var search = new Mock<ICaptureMemorySearchService>();
        var changes = search.As<ICaptureMemorySearchChangeNotifier>();
        search.Setup(value => value.SearchAsync(
                It.IsAny<CaptureMemorySearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => matches);
        using CaptureMemoryHomeViewModel viewModel = CreateViewModel(search.Object);
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SearchQuery = "recognized words";
        await viewModel.SearchCompletion;
        Assert.IsTrue(viewModel.ShowNoMatches);

        for (int cycle = 0; cycle < 3; cycle++)
        {
            matches = [CreateResult(id, 1, CaptureMemoryMatchKind.OcrText, "recognized words")];
            changes.Raise(value => value.SearchIndexChanged += null, EventArgs.Empty);
            await viewModel.SearchCompletion;
            Assert.HasCount(1, viewModel.Results);
            Assert.AreEqual("recognized words", viewModel.SearchQuery);
            matches = [];
            changes.Raise(value => value.SearchIndexChanged += null, EventArgs.Empty);
            await viewModel.SearchCompletion;
            Assert.IsEmpty(viewModel.Results);
        }

        viewModel.Dispose();
        search.Invocations.Clear();
        changes.Raise(value => value.SearchIndexChanged += null, EventArgs.Empty);
        search.Verify(value => value.SearchAsync(It.IsAny<CaptureMemorySearchRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Search_CancelsStaleResultsAndMapsExplanationsAndMissingSources()
    {
        CaptureId staleId = CaptureId.New();
        CaptureId textId = CaptureId.New();
        CaptureId visualId = CaptureId.New();
        CaptureId filenameId = CaptureId.New();
        var staleCompletion = new TaskCompletionSource<IReadOnlyList<CaptureMemorySearchResult>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var search = new Mock<ICaptureMemorySearchService>();
        search.Setup(value => value.SearchAsync(
                It.Is<CaptureMemorySearchRequest>(request => request.Query == "first"),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<CaptureMemorySearchResult>>(staleCompletion.Task));
        search.Setup(value => value.SearchAsync(
                It.Is<CaptureMemorySearchRequest>(request => request.Query == "second"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateResult(textId, 1, CaptureMemoryMatchKind.OcrText, "recognized words"),
                CreateResult(visualId, 2, CaptureMemoryMatchKind.ImageDescription, "a red bicycle"),
                CreateResult(filenameId, 3, CaptureMemoryMatchKind.Filename, "receipt.png"),
            ]);
        var resolver = new Mock<ICaptureMemoryResultResolver>();
        resolver.Setup(value => value.ResolveAsync(It.IsAny<CaptureId>(), It.IsAny<CancellationToken>()))
            .Returns<CaptureId, CancellationToken>((id, _) => ValueTask.FromResult(
                new CaptureMemoryResultLocation(
                    id,
                    id == visualId
                        ? CaptureMemoryResultLocationStatus.SourceMissing
                        : CaptureMemoryResultLocationStatus.Available,
                    $"{id}.png",
                    id == visualId ? null : $@"C:\Captures\{id}.png")));
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(search.Object, resolver.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.SearchQuery = "first";
        Task staleSearch = viewModel.SearchCompletion;
        await Task.Delay(250);
        viewModel.SearchQuery = "second";
        await viewModel.SearchCompletion;
        staleCompletion.SetResult([CreateResult(staleId, 1, CaptureMemoryMatchKind.Filename, "stale.png")]);
        await staleSearch;

        Assert.HasCount(3, viewModel.Results);
        Assert.AreEqual("Text match", viewModel.Results[0].ExplanationLabel);
        Assert.AreEqual("Visual match", viewModel.Results[1].ExplanationLabel);
        Assert.AreEqual("Filename match", viewModel.Results[2].ExplanationLabel);
        Assert.IsTrue(viewModel.Results[1].IsSourceMissing);
        Assert.IsTrue(viewModel.HasSourceMissingResults);
        Assert.IsFalse(viewModel.Results.Any(result => result.CaptureId == staleId));
    }

    [TestMethod]
    public async Task Search_ReportsCorruptProjectionAndOffersNoMatchOnlyForSuccessfulEmptyResult()
    {
        var search = new Mock<ICaptureMemorySearchService>();
        search.Setup(value => value.SearchAsync(
                It.Is<CaptureMemorySearchRequest>(request => request.Query == "broken"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("invalid projection"));
        search.Setup(value => value.SearchAsync(
                It.Is<CaptureMemorySearchRequest>(request => request.Query == "missing"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(search.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.SearchQuery = "broken";
        await viewModel.SearchCompletion;
        Assert.IsTrue(viewModel.HasCorruptProjection);
        Assert.IsFalse(viewModel.ShowNoMatches);

        viewModel.SearchQuery = "missing";
        await viewModel.SearchCompletion;
        Assert.IsFalse(viewModel.HasCorruptProjection);
        Assert.IsTrue(viewModel.ShowNoMatches);
    }

    [TestMethod]
    public async Task Search_ReportsAnIndividualResultResolutionFailure()
    {
        CaptureId captureId = CaptureId.New();
        var search = new Mock<ICaptureMemorySearchService>();
        search.Setup(value => value.SearchAsync(It.IsAny<CaptureMemorySearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateResult(captureId, 1, CaptureMemoryMatchKind.OcrText, "matching text")]);
        var resolver = new Mock<ICaptureMemoryResultResolver>();
        resolver.Setup(value => value.ResolveAsync(captureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureMemoryResultLocation(
                captureId,
                CaptureMemoryResultLocationStatus.Unavailable,
                "capture.png"));
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(search.Object, resolver.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.SearchQuery = "matching";
        await viewModel.SearchCompletion;

        Assert.IsTrue(viewModel.HasFailedResults);
        Assert.HasCount(1, viewModel.Results);
        Assert.IsTrue(viewModel.Results[0].IsResolutionFailed);
        Assert.IsFalse(viewModel.Results[0].CanOpen);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Enable_ShouldDelegateToSharedWorkflowAndRefreshExistingQuery(bool includeExisting)
    {
        var workflow = new TestCaptureMemoryWorkflow { Current = new(CreatePolicySnapshot(false), null) };
        workflow.Execute = request =>
        {
            var result = TestCaptureMemoryWorkflow.Operation(request.Kind, CaptureMemoryOperationStatus.Succeeded,
                scheduled: includeExisting);
            workflow.Current = new(CreatePolicySnapshot(true), result);
            workflow.Publish();
            return Task.FromResult(result);
        };
        var search = new Mock<ICaptureMemorySearchService>();
        search.Setup(s => s.SearchAsync(It.IsAny<CaptureMemorySearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        using var vm = new CaptureMemoryHomeViewModel(new EnabledCaptureMemoryFeatureAvailability(),
            search.Object, CreateAvailableResolver(), workflow: workflow);
        await vm.LoadAsync(CancellationToken.None);
        vm.SearchQuery = "recognized words";
        vm.IncludeExistingCaptures = includeExisting;
        await vm.EnableCaptureMemoryCommand.ExecuteAsync(null);
        await vm.SearchCompletion;
        Assert.AreEqual(includeExisting, workflow.Requests.Single().IncludeExistingCaptures);
        Assert.IsTrue(vm.ShowSearch);
        Assert.IsFalse(vm.IsPreparing);
        search.Verify(s => s.SearchAsync(It.IsAny<CaptureMemorySearchRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [TestMethod]
    public async Task Load_ShouldObserveAppWorkWithoutStartingOrResumingIt()
    {
        var workflow = new TestCaptureMemoryWorkflow { Current = new(CreatePolicySnapshot(true),
            TestCaptureMemoryWorkflow.Operation(CaptureMemoryOperationKind.Reanalyze,
                phase: CaptureMemoryOperationPhase.SchedulingCaptures), .5) };
        using var vm = new CaptureMemoryHomeViewModel(new EnabledCaptureMemoryFeatureAvailability(), workflow: workflow);
        await vm.LoadAsync(CancellationToken.None);
        Assert.IsTrue(vm.ShowSearch, "Reanalysis must not hide usable search.");
        Assert.IsTrue(vm.IsIndexing);
        Assert.IsFalse(vm.CanChangeSetupOptions);
        Assert.IsEmpty(workflow.Requests);
        vm.Dispose();
        Assert.IsEmpty(workflow.Cancellations);
    }

    [TestMethod]
    public async Task RemoveFromMemory_ShouldConfirmForgetHistoryWithoutDeletingSource()
    {
        CaptureId captureId = CaptureId.New();
        var search = new Mock<ICaptureMemorySearchService>();
        search.Setup(value => value.SearchAsync(
                It.IsAny<CaptureMemorySearchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateResult(captureId, 1, CaptureMemoryMatchKind.OcrText, "text")]);
        var resolver = new Mock<ICaptureMemoryResultResolver>();
        resolver.Setup(value => value.ResolveAsync(captureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureMemoryResultLocation(
                captureId,
                CaptureMemoryResultLocationStatus.Available,
                "capture.png",
                @"C:\Captures\capture.png",
                canDeleteRetainedSource: false));
        var removal = new Mock<ICaptureAssetRemovalService>(MockBehavior.Strict);
        removal.Setup(value => value.RemoveAsync(
                It.Is<CaptureAssetRemovalRequest>(request =>
                    request.CaptureId == captureId &&
                    request.Kind == CaptureAssetRemovalKind.ForgetHistory &&
                    !request.IsConfirmed),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CaptureAssetRemovalRequest request, CancellationToken _) =>
                new CaptureAssetRemovalResult(CaptureAssetRemovalStatus.Succeeded, request));
        var confirmation = CreateConfirmation(CaptureAnalysisSettingsAction.RemoveFromMemory);
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(
            search.Object,
            resolver.Object,
            assetRemovalService: removal.Object,
            confirmationService: confirmation.Object);
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SearchQuery = "text";
        await viewModel.SearchCompletion;

        await viewModel.RemoveResultCommand.ExecuteAsync(viewModel.Results.Single());

        Assert.IsEmpty(viewModel.Results);
        removal.VerifyAll();
        confirmation.VerifyAll();
    }

    [TestMethod]
    public async Task DeleteCapture_ShouldBeExposedOnlyForAppOwnedRetainedSourceAndPassConfirmation()
    {
        CaptureId captureId = CaptureId.New();
        var search = new Mock<ICaptureMemorySearchService>();
        search.Setup(value => value.SearchAsync(
                It.IsAny<CaptureMemorySearchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateResult(captureId, 1, CaptureMemoryMatchKind.Filename, "capture.png")]);
        var resolver = new Mock<ICaptureMemoryResultResolver>();
        resolver.Setup(value => value.ResolveAsync(captureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureMemoryResultLocation(
                captureId,
                CaptureMemoryResultLocationStatus.Available,
                "capture.png",
                @"C:\Captures\capture.png",
                canDeleteRetainedSource: true));
        var removal = new Mock<ICaptureAssetRemovalService>(MockBehavior.Strict);
        removal.Setup(value => value.RemoveAsync(
                It.Is<CaptureAssetRemovalRequest>(request =>
                    request.CaptureId == captureId &&
                    request.Kind == CaptureAssetRemovalKind.DeleteRetainedSource &&
                    request.IsConfirmed),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CaptureAssetRemovalRequest request, CancellationToken _) =>
                new CaptureAssetRemovalResult(CaptureAssetRemovalStatus.Succeeded, request));
        var confirmation = CreateConfirmation(CaptureAnalysisSettingsAction.DeleteCapture);
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(
            search.Object,
            resolver.Object,
            assetRemovalService: removal.Object,
            confirmationService: confirmation.Object);
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SearchQuery = "capture";
        await viewModel.SearchCompletion;

        CaptureMemorySearchResultViewModel result = viewModel.Results.Single();
        Assert.IsTrue(result.CanDeleteCapture);
        await viewModel.DeleteResultCommand.ExecuteAsync(result);

        Assert.IsEmpty(viewModel.Results);
        removal.VerifyAll();
        confirmation.VerifyAll();
    }

    [TestMethod]
    public async Task AudioTranscriptResult_ShouldExposeMediaStateAndTimecodeWithoutThumbnail()
    {
        CaptureId captureId = CaptureId.New();
        var search = new Mock<ICaptureMemorySearchService>();
        search.Setup(value => value.SearchAsync(
                It.IsAny<CaptureMemorySearchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateResult(
                captureId,
                1,
                CaptureMemoryMatchKind.SpeechTranscript,
                "deploy the audio pipeline",
                CaptureMediaKind.Audio,
                TimeSpan.FromSeconds(72))]);
        var resolver = new Mock<ICaptureMemoryResultResolver>();
        resolver.Setup(value => value.ResolveAsync(captureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureMemoryResultLocation(
                captureId,
                CaptureMemoryResultLocationStatus.Available,
                "standup.wav",
                @"C:\Captures\standup.wav"));
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(search.Object, resolver.Object);
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SearchQuery = "audio";

        await viewModel.SearchCompletion;

        CaptureMemorySearchResultViewModel result = viewModel.Results.Single();
        Assert.IsTrue(result.IsAudio);
        Assert.IsFalse(result.IsImage);
        Assert.IsFalse(result.IsVideo);
        Assert.IsFalse(result.CanLoadThumbnail);
        Assert.AreEqual("1:12", result.TimecodeLabel);
        Assert.IsTrue(result.HasTimecode);
        Assert.AreEqual("Transcript match", result.ExplanationLabel);
        StringAssert.Contains(result.AutomationName, "1:12");
    }

    [TestMethod]
    public async Task VideoOcrResult_ShouldExposeVideoThumbnailAndTextTimecode()
    {
        CaptureId captureId = CaptureId.New();
        var search = new Mock<ICaptureMemorySearchService>();
        search.Setup(value => value.SearchAsync(
                It.IsAny<CaptureMemorySearchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateResult(
                captureId,
                1,
                CaptureMemoryMatchKind.VideoOcrText,
                "deployment dashboard",
                CaptureMediaKind.Video,
                TimeSpan.FromSeconds(3.5))]);
        var resolver = new Mock<ICaptureMemoryResultResolver>();
        resolver.Setup(value => value.ResolveAsync(captureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureMemoryResultLocation(
                captureId,
                CaptureMemoryResultLocationStatus.Available,
                "demo.mp4",
                @"C:\Captures\demo.mp4"));
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(search.Object, resolver.Object);
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SearchQuery = "dashboard";

        await viewModel.SearchCompletion;

        CaptureMemorySearchResultViewModel result = viewModel.Results.Single();
        Assert.IsTrue(result.IsVideo);
        Assert.IsTrue(result.CanLoadThumbnail);
        Assert.AreEqual("0:03", result.TimecodeLabel);
        Assert.AreEqual("Text match", result.ExplanationLabel);
        Assert.IsFalse(result.HasOcrBounds);
    }

    [TestMethod]
    public async Task VideoDescriptionResult_ShouldExposeVisualMatchAndTimecode()
    {
        CaptureId captureId = CaptureId.New();
        var search = new Mock<ICaptureMemorySearchService>();
        search.Setup(value => value.SearchAsync(
                It.IsAny<CaptureMemorySearchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateResult(
                captureId,
                1,
                CaptureMemoryMatchKind.VideoDescription,
                "a person points at the deployment graph",
                CaptureMediaKind.Video,
                TimeSpan.FromSeconds(30))]);
        var resolver = new Mock<ICaptureMemoryResultResolver>();
        resolver.Setup(value => value.ResolveAsync(captureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureMemoryResultLocation(
                captureId,
                CaptureMemoryResultLocationStatus.Available,
                "walkthrough.mp4",
                @"C:\Captures\walkthrough.mp4"));
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(search.Object, resolver.Object);
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SearchQuery = "deployment graph";

        await viewModel.SearchCompletion;

        CaptureMemorySearchResultViewModel result = viewModel.Results.Single();
        Assert.IsTrue(result.IsVideo);
        Assert.IsTrue(result.CanLoadThumbnail);
        Assert.AreEqual("0:30", result.TimecodeLabel);
        Assert.AreEqual("Visual match", result.ExplanationLabel);
        Assert.AreEqual(CaptureMemoryMatchKind.VideoDescription, result.MatchKind);
    }

    private static CaptureMemoryHomeViewModel CreateViewModel(
        ICaptureMemorySearchService searchService,
        ICaptureMemoryResultResolver? resolver = null,
        ICaptureAnalysisPolicyService? policyService = null,
        ICaptureAssetRemovalService? assetRemovalService = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null)
    {
        policyService ??= CreateAuthorizedPolicyService();
        resolver ??= CreateAvailableResolver();

        return new CaptureMemoryHomeViewModel(
            featureAvailability: new EnabledCaptureMemoryFeatureAvailability(),
            searchService: searchService,
            resultResolver: resolver,
            workflow: new TestCaptureMemoryWorkflow
            {
                Read = async token => new(await policyService.GetCurrentAsync(token), null),
            },
            assetRemovalService: assetRemovalService,
            confirmationService: confirmationService);
    }

    private static Mock<ICaptureAnalysisSettingsConfirmationDialogService> CreateConfirmation(
        CaptureAnalysisSettingsAction expectedAction)
    {
        var confirmation = new Mock<ICaptureAnalysisSettingsConfirmationDialogService>(
            MockBehavior.Strict);
        confirmation.Setup(value => value.ConfirmAsync(
                It.Is<CaptureAnalysisSettingsConfirmationRequest>(request =>
                    request.Action == expectedAction),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CaptureAnalysisConfirmationDecision.Confirmed);
        return confirmation;
    }

    private static ICaptureAnalysisPolicyService CreateAuthorizedPolicyService()
    {
        var policy = new Mock<ICaptureAnalysisPolicyService>();
        policy.Setup(value => value.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePolicySnapshot(authorized: true));
        return policy.Object;
    }

    private static ICaptureMemoryResultResolver CreateAvailableResolver()
    {
        var resolver = new Mock<ICaptureMemoryResultResolver>();
        resolver.Setup(value => value.ResolveAsync(It.IsAny<CaptureId>(), It.IsAny<CancellationToken>()))
            .Returns<CaptureId, CancellationToken>((id, _) => ValueTask.FromResult(
                new CaptureMemoryResultLocation(
                    id,
                    CaptureMemoryResultLocationStatus.Available,
                    $"{id}.png",
                    $@"C:\Captures\{id}.png")));
        return resolver.Object;
    }

    private static CaptureAnalysisPolicySnapshot CreatePolicySnapshot(bool authorized)
    {
        CaptureAnalysisPolicy policy = authorized
            ? CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
                CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
                0)
            : CaptureAnalysisPolicy.Unknown;
        return CreatePolicySnapshot(policy);
    }

    private static CaptureAnalysisPolicySnapshot CreatePolicySnapshot(CaptureAnalysisPolicy policy)
    {
        var control = new CaptureAnalysisControlSnapshot(
            1,
            new CaptureAnalysisControlState(policy, []));
        return new CaptureAnalysisPolicySnapshot(
            CaptureAnalysisPolicySnapshotStatus.Available,
            policy.IsProcessingAuthorized
                ? CaptureAnalysisConsentState.Granted
                : CaptureAnalysisConsentState.Unknown,
            control);
    }

    private static CaptureMemorySearchResult CreateResult(
        CaptureId id,
        int rank,
        CaptureMemoryMatchKind matchKind,
        string snippet,
        CaptureMediaKind mediaKind = CaptureMediaKind.Image,
        TimeSpan? timecode = null)
    {
        return new CaptureMemorySearchResult(
            id,
            mediaKind,
            DateTimeOffset.UtcNow,
            1,
            rank,
            new CaptureMemoryMatchEvidence(matchKind, snippet, timecode: timecode));
    }

    private static AnalyzerIdentity CreateAnalyzerIdentity()
    {
        return new AnalyzerIdentity(
            "test-analyzer",
            "test-provider",
            "test-model",
            "1",
            "1",
            "test-runtime",
            "1",
            "1",
            null);
    }

    private sealed class EnabledCaptureMemoryFeatureAvailability : ICaptureMemoryFeatureAvailability
    {
        public bool IsCaptureMemorySearchEnabled => true;
    }
}
