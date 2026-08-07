using CaptureTool.Application.Abstractions.Analysis.Memory;
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
    public async Task EnableFutureCaptureMemory_AllowsOptionalDescriptionToBeUnsupported()
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
        Assert.IsTrue(viewModel.IsDescriptionUnsupported);
        Assert.IsFalse(viewModel.HasSetupFailure);
    }

    private static CaptureMemoryHomeViewModel CreateViewModel(
        ICaptureMemorySearchService searchService,
        ICaptureMemoryResultResolver? resolver = null,
        ICaptureAnalysisPolicyService? policyService = null,
        ICaptureAnalysisPolicyCommandService? policyCommandService = null,
        IUserInitiatedAnalysisCapabilityPreparationService? preparationService = null)
    {
        policyService ??= CreateAuthorizedPolicyService();
        resolver ??= CreateAvailableResolver();

        return new CaptureMemoryHomeViewModel(
            featureAvailability: new EnabledCaptureMemoryFeatureAvailability(),
            searchService: searchService,
            resultResolver: resolver,
            policyService: policyService,
            policyCommandService: policyCommandService,
            preparationService: preparationService);
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
        var control = new CaptureAnalysisControlSnapshot(
            1,
            new CaptureAnalysisControlState(policy, []));
        return new CaptureAnalysisPolicySnapshot(
            CaptureAnalysisPolicySnapshotStatus.Available,
            authorized ? CaptureAnalysisConsentState.Granted : CaptureAnalysisConsentState.Unknown,
            control);
    }

    private static CaptureMemorySearchResult CreateResult(
        CaptureId id,
        int rank,
        CaptureMemoryMatchKind matchKind,
        string snippet)
    {
        return new CaptureMemorySearchResult(
            id,
            CaptureMediaKind.Image,
            DateTimeOffset.UtcNow,
            1,
            rank,
            new CaptureMemoryMatchEvidence(matchKind, snippet));
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
