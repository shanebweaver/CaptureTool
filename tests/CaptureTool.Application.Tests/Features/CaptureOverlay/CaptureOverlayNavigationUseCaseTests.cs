using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Features.Windowing.ShowMainWindow;
using CaptureTool.Domain.Capture;
using Moq;
using System.Drawing;

namespace CaptureTool.Application.Tests.Features.CaptureOverlay;

[TestClass]
public sealed class CaptureOverlayNavigationUseCaseTests
{
    [TestMethod]
    public async Task OpenOverlayUseCases_NavigateToExpectedOverlayRoutes()
    {
        var navigation = new Mock<INavigationService>();
        var captureOptions = CaptureOptions.VideoDefault;
        NewCaptureArgs captureArgs = CreateCaptureArgs();

        var openSelection = new OpenSelectionOverlayUseCase(navigation.Object, TestUseCaseExecutor.Instance);
        var openCapture = new OpenCaptureOverlayUseCase(navigation.Object, TestUseCaseExecutor.Instance);

        await openSelection.ExecuteAsync(new OpenSelectionOverlayRequest(captureOptions), TestContext.CancellationToken);
        await openCapture.ExecuteAsync(new OpenCaptureOverlayRequest(captureArgs), TestContext.CancellationToken);

        navigation.Verify(service => service.Navigate(NavigationRoute.SelectionOverlay, captureOptions, false), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.CaptureOverlay, captureArgs, false), Times.Once);
    }

    [TestMethod]
    public async Task StopVideoCaptureUseCase_StopsCaptureAndNavigatesToVideoEdit()
    {
        var pendingVideo = new PendingVideoFile("capture.mp4");
        var videoCapture = new Mock<IVideoCaptureHandler>();
        var navigation = new Mock<INavigationService>();
        videoCapture.Setup(handler => handler.StopVideoCapture()).Returns(pendingVideo);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        var useCase = new StopVideoCaptureUseCase(navigation.Object, videoCapture.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new StopVideoCaptureRequest()));
        StopVideoCaptureResponse response = (await useCase.ExecuteAsync(new StopVideoCaptureRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        navigation.Verify(service => service.Navigate(NavigationRoute.VideoEdit, pendingVideo, false), Times.Once);
    }

    [TestMethod]
    public async Task GoBackFromCaptureOverlayUseCase_WhenBackFails_NavigatesToSelectionOverlayAndReportsCancelResult()
    {
        var videoCapture = new Mock<IVideoCaptureHandler>();
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        navigation.Setup(service => service.TryGoBack()).Returns(false);
        var useCase = new GoBackFromCaptureOverlayUseCase(videoCapture.Object, navigation.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new GoBackFromCaptureOverlayRequest()));
        GoBackFromCaptureOverlayResponse response = (await useCase.ExecuteAsync(new GoBackFromCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.VideoCaptureCanceled);
        videoCapture.Verify(handler => handler.CancelVideoCapture(), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.SelectionOverlay, CaptureOptions.VideoDefault, true), Times.Once);
    }

    [TestMethod]
    public async Task GoBackFromCaptureOverlayUseCase_WhenCancelThrows_ReportsNotCanceled()
    {
        var videoCapture = new Mock<IVideoCaptureHandler>();
        var navigation = new Mock<INavigationService>();
        videoCapture.Setup(handler => handler.CancelVideoCapture()).Throws<InvalidOperationException>();
        navigation.Setup(service => service.TryGoBack()).Returns(true);
        var useCase = new GoBackFromCaptureOverlayUseCase(videoCapture.Object, navigation.Object, TestUseCaseExecutor.Instance);

        GoBackFromCaptureOverlayResponse response = (await useCase.ExecuteAsync(new GoBackFromCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.VideoCaptureCanceled);
        navigation.Verify(service => service.Navigate(It.IsAny<object>(), It.IsAny<object?>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task CloseCaptureOverlayUseCase_CancelsCaptureAndShowsMainWindow()
    {
        var videoCapture = new Mock<IVideoCaptureHandler>();
        var showMainWindow = new Mock<IShowMainWindowUseCase>();
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        showMainWindow
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<ShowMainWindowRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<ShowMainWindowResponse>.Success(new ShowMainWindowResponse()));
        var useCase = new CloseCaptureOverlayUseCase(videoCapture.Object, showMainWindow.Object, navigation.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new CloseCaptureOverlayRequest()));
        CloseCaptureOverlayResponse response = (await useCase.ExecuteAsync(new CloseCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.VideoCaptureCanceled);
        videoCapture.Verify(handler => handler.CancelVideoCapture(), Times.Once);
        showMainWindow.Verify(useCase => useCase.ExecuteAsync(It.IsAny<ShowMainWindowRequest>(), TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task ToggleVideoCaptureDesktopAudioUseCase_TogglesHandlerState()
    {
        var videoCapture = new Mock<IVideoCaptureHandler>();
        videoCapture.Setup(handler => handler.IsDesktopAudioEnabled).Returns(false);
        var useCase = new ToggleVideoCaptureDesktopAudioUseCase(videoCapture.Object, TestUseCaseExecutor.Instance);

        ToggleVideoCaptureDesktopAudioResponse response = (await useCase.ExecuteAsync(new ToggleVideoCaptureDesktopAudioRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        videoCapture.Verify(handler => handler.SetIsDesktopAudioEnabled(true), Times.Once);
        videoCapture.Verify(handler => handler.ToggleDesktopAudioCapture(true), Times.Once);
    }

    [TestMethod]
    public async Task ToggleVideoCapturePauseResumeUseCase_RequiresRecordingAndTogglesPauseState()
    {
        var videoCapture = new Mock<IVideoCaptureHandler>();
        videoCapture.Setup(handler => handler.IsRecording).Returns(true);
        videoCapture.Setup(handler => handler.IsPaused).Returns(false);
        var useCase = new ToggleVideoCapturePauseResumeUseCase(videoCapture.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new ToggleVideoCapturePauseResumeRequest()));
        ToggleVideoCapturePauseResumeResponse response = (await useCase.ExecuteAsync(new ToggleVideoCapturePauseResumeRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        videoCapture.Verify(handler => handler.ToggleIsPaused(true), Times.Once);
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
