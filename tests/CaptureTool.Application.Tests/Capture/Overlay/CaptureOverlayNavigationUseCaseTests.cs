using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Capture.Overlay.OpenCaptureOverlay;
using CaptureTool.Application.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Capture.Video.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Capture.Video.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Windowing.ShowMainWindow;
using CaptureTool.Application.Tests.Capture.Video;
using CaptureTool.Domain.Capture;
using Moq;
using System.Drawing;

namespace CaptureTool.Application.Tests.Capture.Overlay;

[TestClass]
public sealed class CaptureOverlayNavigationUseCaseTests
{
    [TestMethod]
    public async Task OpenOverlayUseCases_NavigateToExpectedOverlayRoutes()
    {
        var navigation = new Mock<INavigationService>();
        var captureOptions = CaptureOptions.VideoDefault;
        NewCaptureArgs captureArgs = CreateCaptureArgs();
        var audioCaptureNavigationGuard = new AllowAudioCaptureNavigationGuard();

        var openSelection = new OpenSelectionOverlayUseCase(
            navigation.Object,
            TestUseCaseExecutor.Instance,
            audioCaptureNavigationGuard);
        var openCapture = new OpenCaptureOverlayUseCase(
            navigation.Object,
            TestUseCaseExecutor.Instance,
            audioCaptureNavigationGuard);

        await openSelection.ExecuteAsync(new OpenSelectionOverlayRequest(captureOptions), TestContext.CancellationToken);
        await openCapture.ExecuteAsync(new OpenCaptureOverlayRequest(captureArgs), TestContext.CancellationToken);

        navigation.Verify(service => service.Navigate(NavigationRoute.SelectionOverlay, captureOptions, false), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.CaptureOverlay, captureArgs, false), Times.Once);
    }

    [TestMethod]
    public async Task StopVideoCaptureUseCase_StopsCaptureAndNavigatesToVideoEdit()
    {
        var pendingVideo = new PendingVideoFile("capture.mp4");
        var videoCapture = new FakeVideoCaptureWorkflow { PendingVideo = pendingVideo };
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        var useCase = new StopVideoCaptureUseCase(navigation.Object, videoCapture, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new StopVideoCaptureRequest()));
        StopVideoCaptureResponse response = (await useCase.ExecuteAsync(new StopVideoCaptureRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.AreEqual(1, videoCapture.StopCallCount);
        navigation.Verify(service => service.Navigate(NavigationRoute.VideoEdit, pendingVideo, false), Times.Once);
    }

    [TestMethod]
    public async Task CancelVideoCaptureUseCase_WhenCaptureWarningsDisabled_CancelsWithoutPrompt()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        ICancelVideoCaptureUseCase useCase = CreateCancelVideoCaptureUseCase(
            videoCapture,
            shouldWarnBeforeDiscard: false,
            confirmationService: confirmationService.Object);

        CancelVideoCaptureResponse response = (await useCase.ExecuteAsync(
            new CancelVideoCaptureRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.AreEqual(1, videoCapture.CancelCallCount);
        confirmationService.Verify(
            service => service.ConfirmDiscardActiveCaptureAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CancelVideoCaptureUseCase_WhenCaptureWarningsEnabledAndUserConfirms_Cancels()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        confirmationService
            .Setup(service => service.ConfirmDiscardActiveCaptureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        ICancelVideoCaptureUseCase useCase = CreateCancelVideoCaptureUseCase(
            videoCapture,
            shouldWarnBeforeDiscard: true,
            confirmationService: confirmationService.Object);

        CancelVideoCaptureResponse response = (await useCase.ExecuteAsync(
            new CancelVideoCaptureRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.AreEqual(1, videoCapture.CancelCallCount);
        confirmationService.Verify(
            service => service.ConfirmDiscardActiveCaptureAsync(TestContext.CancellationToken),
            Times.Once);
    }

    [TestMethod]
    public async Task CancelVideoCaptureUseCase_WhenCaptureWarningsEnabledAndUserDeclines_DoesNotCancel()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        confirmationService
            .Setup(service => service.ConfirmDiscardActiveCaptureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        ICancelVideoCaptureUseCase useCase = CreateCancelVideoCaptureUseCase(
            videoCapture,
            shouldWarnBeforeDiscard: true,
            confirmationService: confirmationService.Object);

        CancelVideoCaptureResponse response = (await useCase.ExecuteAsync(
            new CancelVideoCaptureRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        Assert.AreEqual(0, videoCapture.CancelCallCount);
        confirmationService.Verify(
            service => service.ConfirmDiscardActiveCaptureAsync(TestContext.CancellationToken),
            Times.Once);
    }

    [TestMethod]
    public async Task GoBackFromCaptureOverlayUseCase_WhenBackFails_NavigatesToSelectionOverlayAndReportsCancelResult()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        navigation.Setup(service => service.TryGoBack()).Returns(false);
        ICancelVideoCaptureUseCase cancelUseCase = CreateCancelVideoCaptureUseCase(videoCapture, shouldWarnBeforeDiscard: false);
        var useCase = new GoBackFromCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase,
            navigation.Object,
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new GoBackFromCaptureOverlayRequest()));
        GoBackFromCaptureOverlayResponse response = (await useCase.ExecuteAsync(new GoBackFromCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.VideoCaptureCanceled);
        Assert.AreEqual(1, videoCapture.CancelCallCount);
        navigation.Verify(service => service.Navigate(NavigationRoute.SelectionOverlay, CaptureOptions.VideoDefault, true), Times.Once);
    }

    [TestMethod]
    public async Task GoBackFromCaptureOverlayUseCase_WhenCancelFails_DoesNotNavigateBack()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        var cancelUseCase = new Mock<ICancelVideoCaptureUseCase>();
        cancelUseCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<CancelVideoCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<CancelVideoCaptureResponse>.Failure());
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.TryGoBack()).Returns(true);
        var useCase = new GoBackFromCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase.Object,
            navigation.Object,
            TestUseCaseExecutor.Instance);

        GoBackFromCaptureOverlayResponse response = (await useCase.ExecuteAsync(new GoBackFromCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.VideoCaptureCanceled);
        navigation.Verify(service => service.TryGoBack(), Times.Never);
        navigation.Verify(service => service.Navigate(It.IsAny<object>(), It.IsAny<object?>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task GoBackFromCaptureOverlayUseCase_WhenRecordingAndUserDeclinesDiscard_DoesNotNavigateBack()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        confirmationService
            .Setup(service => service.ConfirmDiscardActiveCaptureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        ICancelVideoCaptureUseCase cancelUseCase = CreateCancelVideoCaptureUseCase(
            videoCapture,
            shouldWarnBeforeDiscard: true,
            confirmationService: confirmationService.Object);
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        navigation.Setup(service => service.TryGoBack()).Returns(true);
        var useCase = new GoBackFromCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase,
            navigation.Object,
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new GoBackFromCaptureOverlayRequest()));
        GoBackFromCaptureOverlayResponse response = (await useCase.ExecuteAsync(new GoBackFromCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.VideoCaptureCanceled);
        Assert.AreEqual(0, videoCapture.CancelCallCount);
        confirmationService.Verify(
            service => service.ConfirmDiscardActiveCaptureAsync(TestContext.CancellationToken),
            Times.Once);
        navigation.Verify(service => service.TryGoBack(), Times.Never);
        navigation.Verify(service => service.Navigate(It.IsAny<object>(), It.IsAny<object?>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task CloseCaptureOverlayUseCase_CancelsCaptureAndShowsMainWindow()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        ICancelVideoCaptureUseCase cancelUseCase = CreateCancelVideoCaptureUseCase(videoCapture, shouldWarnBeforeDiscard: false);
        var showMainWindow = new Mock<IShowMainWindowUseCase>();
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        showMainWindow
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<ShowMainWindowRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<ShowMainWindowResponse>.Success(new ShowMainWindowResponse()));
        var useCase = new CloseCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase,
            showMainWindow.Object,
            navigation.Object,
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new CloseCaptureOverlayRequest()));
        CloseCaptureOverlayResponse response = (await useCase.ExecuteAsync(new CloseCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.VideoCaptureCanceled);
        Assert.AreEqual(1, videoCapture.CancelCallCount);
        showMainWindow.Verify(useCase => useCase.ExecuteAsync(It.IsAny<ShowMainWindowRequest>(), TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task CloseCaptureOverlayUseCase_WhenRecordingAndUserDeclinesDiscard_DoesNotShowMainWindow()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        confirmationService
            .Setup(service => service.ConfirmDiscardActiveCaptureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        ICancelVideoCaptureUseCase cancelUseCase = CreateCancelVideoCaptureUseCase(
            videoCapture,
            shouldWarnBeforeDiscard: true,
            confirmationService: confirmationService.Object);
        var showMainWindow = new Mock<IShowMainWindowUseCase>();
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        var useCase = new CloseCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase,
            showMainWindow.Object,
            navigation.Object,
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new CloseCaptureOverlayRequest()));
        CloseCaptureOverlayResponse response = (await useCase.ExecuteAsync(new CloseCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.VideoCaptureCanceled);
        Assert.AreEqual(0, videoCapture.CancelCallCount);
        confirmationService.Verify(
            service => service.ConfirmDiscardActiveCaptureAsync(TestContext.CancellationToken),
            Times.Once);
        showMainWindow.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<ShowMainWindowRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ToggleVideoCaptureDesktopAudioUseCase_TogglesHandlerState()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsDesktopAudioEnabled = false };
        var useCase = new ToggleVideoCaptureDesktopAudioUseCase(videoCapture, TestUseCaseExecutor.Instance);

        ToggleVideoCaptureDesktopAudioResponse response = (await useCase.ExecuteAsync(new ToggleVideoCaptureDesktopAudioRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.IsTrue(videoCapture.IsDesktopAudioEnabled);
        Assert.IsTrue(videoCapture.LastDesktopAudioEnabled);
    }

    [TestMethod]
    public async Task ToggleVideoCapturePauseResumeUseCase_RequiresRecordingAndTogglesPauseState()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true, IsPaused = false };
        var useCase = new ToggleVideoCapturePauseResumeUseCase(videoCapture, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new ToggleVideoCapturePauseResumeRequest()));
        ToggleVideoCapturePauseResumeResponse response = (await useCase.ExecuteAsync(new ToggleVideoCapturePauseResumeRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.IsTrue(videoCapture.LastPausedState);
    }

    [TestMethod]
    public async Task ShowMainWindowUseCase_WhenBackToMainWindowFails_NavigatesHomeAndClearsHistory()
    {
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.TryGoBackTo(It.IsAny<Func<INavigationRequest, bool>>())).Returns(false);
        var useCase = new ShowMainWindowUseCase(navigation.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new ShowMainWindowRequest()));
        ShowMainWindowResponse response = (await useCase.ExecuteAsync(new ShowMainWindowRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        navigation.Verify(service => service.Navigate(NavigationRoute.Home, null, true), Times.Once);
    }

    private static INavigationRequest CreateNavigationRequest(NavigationRoute route) =>
        new TestNavigationRequest(route);

    private static ICancelVideoCaptureUseCase CreateCancelVideoCaptureUseCase(
        FakeVideoCaptureWorkflow videoCapture,
        bool shouldWarnBeforeDiscard,
        ICaptureDiscardConfirmationService? confirmationService = null)
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard))
            .Returns(shouldWarnBeforeDiscard);

        return new CancelVideoCaptureUseCase(
            videoCapture,
            confirmationService ?? Mock.Of<ICaptureDiscardConfirmationService>(),
            settings.Object,
            TestUseCaseExecutor.Instance);
    }

    private static NewCaptureArgs CreateCaptureArgs()
    {
        var monitor = new MonitorCaptureResult(
            1,
            [],
            96,
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1040),
            true);

        return new NewCaptureArgs(monitor, new Rectangle(10, 20, 300, 200));
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class TestNavigationRequest(object route) : INavigationRequest
    {
        public object Route { get; } = route;
        public object? Parameter => null;
        public bool IsBackNavigation => false;
        public bool ClearHistory => false;
    }
}
