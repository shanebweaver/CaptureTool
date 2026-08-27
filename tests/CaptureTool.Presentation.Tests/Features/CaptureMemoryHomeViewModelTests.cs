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
    public async Task EnableFutureCaptureMemory_AllowsUnavailableModelsWithLimitedCoverage()
    {
        CaptureAnalysisPolicySnapshot initial = CreatePolicySnapshot(authorized: false);
        CaptureAnalysisPolicySnapshot authorized = CreatePolicySnapshot(authorized: true);
        var policy = new Mock<ICaptureAnalysisPolicyService>();
        policy.Setup(value => value.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(initial);
        var commands = new Mock<ICaptureAnalysisPolicyCommandService>();
        commands.Setup(value => value.ApplyConsentDecisionAsync(
                It.IsAny<CaptureTool.Application.Abstractions.Analysis.Consent.CaptureAnalysisConsentResponse>(),
                initial.ControlDocumentRevision,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisPolicyChangeResult(
                CaptureAnalysisPolicyChangeStatus.Succeeded,
                authorized));
        var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>();
        preparation.Setup(value => value.PrepareAsync(
                It.Is<AnalysisCapabilityPreparationRequest>(request =>
                    request.Capability.Id == AnalysisCapabilities.ImageDescriptionV1.Id),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalysisCapabilityPreparationState.Unsupported(new AnalysisFailure(
                AnalysisFailureCode.CapabilityUnavailable,
                AnalysisFailureDisposition.Terminal)));
        preparation.Setup(value => value.PrepareAsync(
                It.Is<AnalysisCapabilityPreparationRequest>(request =>
                    request.Capability.Id != AnalysisCapabilities.ImageDescriptionV1.Id),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalysisCapabilityPreparationState.Ready(
                CreateAnalyzerIdentity(),
                ProcessingBoundary.OnDevice));
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(
            Mock.Of<ICaptureMemorySearchService>(),
            policyService: policy.Object,
            policyCommandService: commands.Object,
            preparationService: preparation.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.EnableForFutureCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.IsAuthorized);
        Assert.IsTrue(viewModel.HasLimitedModelCoverage);
        Assert.IsFalse(viewModel.HasSetupFailure);
    }

    [TestMethod]
    public async Task EnableExistingCaptureMemory_ShouldStartAuthorizedBackfill()
    {
        CaptureAnalysisPolicySnapshot initial = CreatePolicySnapshot(authorized: false);
        CaptureAnalysisPolicySnapshot authorized = CreatePolicySnapshot(authorized: true);
        CaptureAnalysisPolicy backfillPolicy = authorized.Policy!
            .AuthorizeExistingCaptureBackfill(currentSequence: 12);
        CaptureAnalysisPolicy completedPolicy = backfillPolicy
            .StartExistingCaptureBackfill()
            .AdvanceExistingCaptureBackfill(checkpoint: 12);
        CaptureAnalysisPolicySnapshot backfillAuthorized = CreatePolicySnapshot(backfillPolicy);
        CaptureAnalysisPolicySnapshot completed = CreatePolicySnapshot(completedPolicy);
        var policy = new Mock<ICaptureAnalysisPolicyService>();
        policy.SetupSequence(value => value.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial)
            .ReturnsAsync(initial)
            .ReturnsAsync(completed);
        var commands = new Mock<ICaptureAnalysisPolicyCommandService>();
        commands.Setup(value => value.ApplyConsentDecisionAsync(
                It.IsAny<CaptureTool.Application.Abstractions.Analysis.Consent.CaptureAnalysisConsentResponse>(),
                initial.ControlDocumentRevision,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisPolicyChangeResult(
                CaptureAnalysisPolicyChangeStatus.Succeeded,
                authorized));
        commands.Setup(value => value.AuthorizeExistingCaptureBackfillAsync(
                authorized.ControlDocumentRevision,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisPolicyChangeResult(
                CaptureAnalysisPolicyChangeStatus.Succeeded,
                backfillAuthorized));
        var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>();
        preparation.Setup(value => value.PrepareAsync(
                It.Is<AnalysisCapabilityPreparationRequest>(request =>
                    request.Capability.Id == AnalysisCapabilities.SpeechTranscriptV1.Id),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalysisCapabilityPreparationState.Unsupported(new AnalysisFailure(
                AnalysisFailureCode.CapabilityUnavailable,
                AnalysisFailureDisposition.Terminal)));
        preparation.Setup(value => value.PrepareAsync(
                It.Is<AnalysisCapabilityPreparationRequest>(request =>
                    request.Capability.Id != AnalysisCapabilities.SpeechTranscriptV1.Id),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalysisCapabilityPreparationState.Ready(
                CreateAnalyzerIdentity(),
                ProcessingBoundary.OnDevice));
        var backfill = new Mock<ICaptureAnalysisBackfillService>();
        backfill.Setup(value => value.RunAsync(
                It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisBackfillRunResult(
                CaptureAnalysisBackfillRunStatus.Completed,
                new CaptureAnalysisBackfillProgress(12, 12, 2)));
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(
            Mock.Of<ICaptureMemorySearchService>(),
            policyService: policy.Object,
            policyCommandService: commands.Object,
            preparationService: preparation.Object,
            backfillService: backfill.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.EnableForExistingCommand.ExecuteAsync(null);
        await viewModel.BackfillCompletion;

        Assert.IsTrue(viewModel.IsAuthorized);
        Assert.IsFalse(viewModel.IsIndexing);
        Assert.AreEqual(1, viewModel.IndexProgress);
        Assert.IsTrue(viewModel.HasLimitedModelCoverage);
        Assert.IsFalse(viewModel.HasSetupFailure);
        backfill.Verify(value => value.RunAsync(
            It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Load_ShouldResumeAnAuthorizedBackfill()
    {
        CaptureAnalysisPolicy backfillPolicy = CaptureAnalysisPolicy.Unknown
            .GrantFutureCaptures(
                CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
                currentSequence: 4)
            .AuthorizeExistingCaptureBackfill(currentSequence: 8);
        CaptureAnalysisPolicy completedPolicy = backfillPolicy
            .StartExistingCaptureBackfill()
            .AdvanceExistingCaptureBackfill(checkpoint: 8);
        var policy = new Mock<ICaptureAnalysisPolicyService>();
        policy.SetupSequence(value => value.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePolicySnapshot(backfillPolicy))
            .ReturnsAsync(CreatePolicySnapshot(completedPolicy));
        var backfill = new Mock<ICaptureAnalysisBackfillService>();
        backfill.Setup(value => value.RunAsync(
                It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisBackfillRunResult(
                CaptureAnalysisBackfillRunStatus.Completed,
                new CaptureAnalysisBackfillProgress(8, 8, 1)));
        CaptureMemoryHomeViewModel viewModel = CreateViewModel(
            Mock.Of<ICaptureMemorySearchService>(),
            policyService: policy.Object,
            backfillService: backfill.Object);

        await viewModel.LoadAsync(CancellationToken.None);
        await viewModel.BackfillCompletion;

        Assert.IsFalse(viewModel.IsIndexing);
        Assert.AreEqual(1, viewModel.IndexProgress);
        Assert.IsFalse(viewModel.HasSetupFailure);
        backfill.Verify(value => value.RunAsync(
            It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
        ICaptureAnalysisPolicyCommandService? policyCommandService = null,
        IUserInitiatedAnalysisCapabilityPreparationService? preparationService = null,
        ICaptureAssetRemovalService? assetRemovalService = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null,
        ICaptureAnalysisBackfillService? backfillService = null)
    {
        policyService ??= CreateAuthorizedPolicyService();
        resolver ??= CreateAvailableResolver();

        return new CaptureMemoryHomeViewModel(
            featureAvailability: new EnabledCaptureMemoryFeatureAvailability(),
            searchService: searchService,
            resultResolver: resolver,
            policyService: policyService,
            policyCommandService: policyCommandService,
            preparationService: preparationService,
            assetRemovalService: assetRemovalService,
            confirmationService: confirmationService,
            backfillService: backfillService);
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
