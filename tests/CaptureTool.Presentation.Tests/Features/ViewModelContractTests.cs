using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Feedback;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.About;
using CaptureTool.Presentation.Features.Home;
using CaptureTool.Presentation.Features.RecentCaptures;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ViewModelContractTests
{
    [TestMethod]
    public void AboutPageViewModel_ShouldRaiseDialogRequest_WithLocalizedContent()
    {
        var goBack = Mock.Of<ILeaveAboutPageUseCase>();
        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetString("About_ThirdParty_DialogTitle")).Returns("Third-party");
        localization.Setup(service => service.GetString("About_ThirdParty_DialogContent")).Returns("Notices");

        var viewModel = new AboutPageViewModel(goBack, Mock.Of<IFeedbackHubService>(), localization.Object);

        (string title, string content)? dialog = null;
        viewModel.ShowDialogRequested += (_, args) => dialog = args;

        viewModel.ShowThirdPartyCommand.Execute(null);

        Assert.IsNotNull(dialog);
        Assert.AreEqual("Third-party", dialog.Value.title);
        Assert.AreEqual("Notices", dialog.Value.content);
    }

    [TestMethod]
    public async Task AboutPageViewModel_SendFeedbackCommand_ShouldLaunchFeedbackHub()
    {
        var feedbackHub = new Mock<IFeedbackHubService>();
        feedbackHub
            .Setup(service => service.LaunchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var viewModel = new AboutPageViewModel(
            Mock.Of<ILeaveAboutPageUseCase>(),
            feedbackHub.Object,
            Mock.Of<ILocalizationService>());

        await viewModel.SendFeedbackCommand.ExecuteAsync(null);

        feedbackHub.Verify(
            service => service.LaunchAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HomePageViewModel_NewImageCaptureCommand_ShouldExecuteSelectionOverlayUseCase()
    {
        var openSelectionOverlay = new Mock<IOpenSelectionOverlayUseCase>();
        var openAudioCapturePage = Mock.Of<IOpenAudioCapturePageUseCase>();

        var viewModel = CreateHomePageViewModel(
            openSelectionOverlayUseCase: openSelectionOverlay.Object,
            openAudioCapturePageUseCase: openAudioCapturePage);

        viewModel.NewImageCaptureCommand.Execute(null);

        await Task.Yield();

        openSelectionOverlay.Verify(
            useCase => useCase.ExecuteAsync(
                It.Is<OpenSelectionOverlayRequest>(request => request.CaptureOptions.CaptureMode == CaptureMode.Image),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HomePageViewModel_LoadAsync_WhenStoreReviewEligible_ShouldRequestPrompt()
    {
        var appMetrics = new Mock<IAppMetricsService>();
        appMetrics
            .Setup(service => service.ShouldShowStoreReviewReminder())
            .Returns(true);
        var viewModel = CreateHomePageViewModel(appMetricsService: appMetrics.Object);
        int requestCount = 0;
        viewModel.StoreReviewPromptRequested += (_, _) => requestCount++;

        await viewModel.LoadAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, requestCount);
    }

    [TestMethod]
    public async Task HomePageViewModel_LeaveStoreReviewCommand_WhenStoreLaunchSucceeds_ShouldDisableReminders()
    {
        var appMetrics = new Mock<IAppMetricsService>();
        var storeService = new Mock<IStoreService>();
        storeService
            .Setup(service => service.LaunchAppReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var viewModel = CreateHomePageViewModel(
            appMetricsService: appMetrics.Object,
            storeService: storeService.Object);

        await viewModel.LeaveStoreReviewCommand.ExecuteAsync(null);

        appMetrics.Verify(
            service => service.SetStoreReviewRemindersEnabledAsync(false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HomePageViewModel_RemindStoreReviewLaterCommand_ShouldResetReminderCriteria()
    {
        var appMetrics = new Mock<IAppMetricsService>();
        var viewModel = CreateHomePageViewModel(appMetricsService: appMetrics.Object);

        await viewModel.RemindStoreReviewLaterCommand.ExecuteAsync(null);

        appMetrics.Verify(
            service => service.RemindAboutStoreReviewLaterAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HomePageViewModel_LoadMoreRecentCapturesCommand_ShouldAppendNextPage()
    {
        var getRecentCapturesUseCase = new Mock<IGetRecentCapturesUseCase>();
        RecentCapture[] firstPage = Enumerable.Range(0, 24)
            .Select(index => new RecentCapture($@"C:\Temp\capture-{index}.png", $"capture-{index}.png", CaptureFileType.Image))
            .ToArray();
        var secondPage = new RecentCapture(@"C:\Temp\capture-24.png", "capture-24.png", CaptureFileType.Image);

        getRecentCapturesUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                It.Is<GetRecentCapturesRequest>(request => request.Skip == 0 && request.Take == 24),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<GetRecentCapturesResponse>.Success(new GetRecentCapturesResponse(firstPage, HasMore: true)));
        getRecentCapturesUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                It.Is<GetRecentCapturesRequest>(request => request.Skip == 24 && request.Take == 24),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<GetRecentCapturesResponse>.Success(new GetRecentCapturesResponse([secondPage], HasMore: false)));

        var viewModel = CreateHomePageViewModel(getRecentCapturesUseCase: getRecentCapturesUseCase.Object);

        await viewModel.LoadAsync(TestContext.CancellationToken);
        await viewModel.LoadMoreRecentCapturesCommand.ExecuteAsync(null);

        Assert.HasCount(25, viewModel.RecentCaptures);
        Assert.AreEqual(secondPage.FilePath, viewModel.RecentCaptures[^1].FilePath);
        Assert.IsFalse(viewModel.HasMoreRecentCaptures);
    }

    private static HomePageViewModel CreateHomePageViewModel(
        IOpenSelectionOverlayUseCase? openSelectionOverlayUseCase = null,
        IOpenAudioCapturePageUseCase? openAudioCapturePageUseCase = null,
        IAppMetricsService? appMetricsService = null,
        IStoreService? storeService = null,
        IGetRecentCapturesUseCase? getRecentCapturesUseCase = null,
        IOpenRecentCaptureUseCase? openRecentCaptureUseCase = null,
        IImageCaptureState? imageCaptureState = null,
        IVideoCaptureState? videoCaptureState = null,
        IAudioCaptureState? audioCaptureState = null,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string>? recentCaptureViewModelFactory = null)
    {
        var fallbackGetRecentCapturesUseCase = new Mock<IGetRecentCapturesUseCase>();
        fallbackGetRecentCapturesUseCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<GetRecentCapturesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<GetRecentCapturesResponse>.Success(new GetRecentCapturesResponse([])));

        var fallbackRecentCaptureViewModelFactory = new Mock<IFactoryServiceWithArgs<RecentCaptureViewModel, string>>();
        fallbackRecentCaptureViewModelFactory
            .Setup(factory => factory.Create(It.IsAny<string>()))
            .Returns<string>(filePath => new RecentCaptureViewModel(filePath));

        return new HomePageViewModel(
            openSelectionOverlayUseCase ?? Mock.Of<IOpenSelectionOverlayUseCase>(),
            openAudioCapturePageUseCase ?? Mock.Of<IOpenAudioCapturePageUseCase>(),
            appMetricsService ?? Mock.Of<IAppMetricsService>(),
            storeService ?? Mock.Of<IStoreService>(),
            getRecentCapturesUseCase ?? fallbackGetRecentCapturesUseCase.Object,
            openRecentCaptureUseCase ?? Mock.Of<IOpenRecentCaptureUseCase>(),
            imageCaptureState ?? Mock.Of<IImageCaptureState>(),
            videoCaptureState ?? Mock.Of<IVideoCaptureState>(),
            audioCaptureState ?? Mock.Of<IAudioCaptureState>(),
            recentCaptureViewModelFactory ?? fallbackRecentCaptureViewModelFactory.Object);
    }

    public TestContext TestContext { get; set; } = null!;
}
