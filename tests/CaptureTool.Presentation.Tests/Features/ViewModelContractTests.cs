using CaptureTool.Application.Abstractions.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Features.About;
using CaptureTool.Presentation.Features.Home;
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

        var viewModel = new AboutPageViewModel(goBack, localization.Object);

        (string title, string content)? dialog = null;
        viewModel.ShowDialogRequested += (_, args) => dialog = args;

        viewModel.ShowThirdPartyCommand.Execute(null);

        Assert.IsNotNull(dialog);
        Assert.AreEqual("Third-party", dialog.Value.title);
        Assert.AreEqual("Notices", dialog.Value.content);
    }

    [TestMethod]
    public async Task HomePageViewModel_NewImageCaptureCommand_ShouldExecuteSelectionOverlayUseCase()
    {
        var openSelectionOverlay = new Mock<IOpenSelectionOverlayUseCase>();
        var openAudioCapturePage = Mock.Of<IOpenAudioCapturePageUseCase>();

        var viewModel = new HomePageViewModel(
            openSelectionOverlay.Object,
            openAudioCapturePage,
            Mock.Of<IAppMetricsService>(),
            Mock.Of<IStoreService>());

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
        var viewModel = new HomePageViewModel(
            Mock.Of<IOpenSelectionOverlayUseCase>(),
            Mock.Of<IOpenAudioCapturePageUseCase>(),
            appMetrics.Object,
            Mock.Of<IStoreService>());
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
        var viewModel = new HomePageViewModel(
            Mock.Of<IOpenSelectionOverlayUseCase>(),
            Mock.Of<IOpenAudioCapturePageUseCase>(),
            appMetrics.Object,
            storeService.Object);

        await viewModel.LeaveStoreReviewCommand.ExecuteAsync(null);

        appMetrics.Verify(
            service => service.SetStoreReviewRemindersEnabledAsync(false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HomePageViewModel_RemindStoreReviewLaterCommand_ShouldResetReminderCriteria()
    {
        var appMetrics = new Mock<IAppMetricsService>();
        var viewModel = new HomePageViewModel(
            Mock.Of<IOpenSelectionOverlayUseCase>(),
            Mock.Of<IOpenAudioCapturePageUseCase>(),
            appMetrics.Object,
            Mock.Of<IStoreService>());

        await viewModel.RemindStoreReviewLaterCommand.ExecuteAsync(null);

        appMetrics.Verify(
            service => service.RemindAboutStoreReviewLaterAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;
}
