using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Abstractions.Windowing.ShowMainWindow;
using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Capture.Overlay.OpenCaptureOverlay;
using CaptureTool.Application.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Capture.Video.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Capture.Video.ToggleVideoCapturePauseResume;
using CaptureTool.Application.EditSessions;
using CaptureTool.Application.Navigation;
using CaptureTool.Application.Tests.Capture.Video;
using CaptureTool.Application.Windowing.ShowMainWindow;
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
        TestNavigationService.AcceptAll(navigation);
        var captureOptions = CaptureOptions.VideoDefault;
        NewCaptureArgs captureArgs = CreateCaptureArgs();
        var audioCaptureNavigationGuard = new AllowAudioCaptureNavigationGuard();
        var cleanEditSession = new Mock<IEditableSession>();
        cleanEditSession.SetupGet(session => session.HasUnsavedChanges).Returns(false);
        var activeEditSession = new Mock<IActiveEditSessionService>();
        activeEditSession.SetupGet(service => service.CurrentSession).Returns(cleanEditSession.Object);
        INavigationCoordinator coordinator = TestNavigationCoordinator.Create(
            navigation.Object,
            audioCaptureNavigationGuard: audioCaptureNavigationGuard);

        var openSelection = new OpenSelectionOverlayUseCase(
            coordinator,
            activeEditSession.Object,
            TestUseCaseExecutor.Instance);
        var openCapture = new OpenCaptureOverlayUseCase(
            coordinator,
            TestUseCaseExecutor.Instance);

        await openSelection.ExecuteAsync(new OpenSelectionOverlayRequest(captureOptions), TestContext.CancellationToken);
        await openCapture.ExecuteAsync(new OpenCaptureOverlayRequest(captureArgs), TestContext.CancellationToken);

        navigation.Verify(service => service.NavigateAsync(NavigationRoute.SelectionOverlay, captureOptions, false, It.IsAny<CancellationToken>()), Times.Once);
        navigation.Verify(service => service.NavigateAsync(NavigationRoute.Home, null, true, It.IsAny<CancellationToken>()), Times.Never);
        navigation.Verify(service => service.NavigateAsync(NavigationRoute.CaptureOverlay, captureArgs, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task OpenSelectionOverlay_WhenDirtyEditLeaveIsAccepted_SeedsHomeAndOverlayCloseReturnsHomeWithoutAnotherPrompt()
    {
        var session = new Mock<IEditableSession>();
        session.SetupGet(value => value.HasUnsavedChanges).Returns(true);
        var activeSession = new ActiveEditSessionService();
        activeSession.SetCurrentSession(session.Object);

        var confirmation = new Mock<IEditSessionConfirmationService>();
        confirmation
            .Setup(service => service.ConfirmLeaveAsync(session.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditSessionLeaveDecision.Discard);
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard))
            .Returns(true);

        INavigationRequest currentRequest = CreateNavigationRequest(NavigationRoute.ImageEdit);
        var navigationOrder = new List<(object Route, bool ClearHistory)>();
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation.SetupGet(service => service.CurrentRequest).Returns(() => currentRequest);
        navigation.SetupGet(service => service.CanGoBack).Returns(true);
        navigation
            .Setup(service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<object, object?, bool, CancellationToken>((route, _, clearHistory, _) =>
            {
                navigationOrder.Add((route, clearHistory));
                currentRequest = CreateNavigationRequest((NavigationRoute)route);
                if (Equals(route, NavigationRoute.Home))
                {
                    activeSession.ClearCurrentSession(session.Object);
                }
            })
            .ReturnsAsync(NavigationResult.Accepted);
        navigation
            .Setup(service => service.TryGoBackToAsync(
                It.IsAny<Func<INavigationRequest, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<INavigationRequest, bool>, CancellationToken>((assessRequest, _) =>
            {
                INavigationRequest homeRequest = CreateNavigationRequest(NavigationRoute.Home);
                Assert.IsTrue(assessRequest(homeRequest));
                currentRequest = homeRequest;
                return Task.FromResult(NavigationResult.Accepted);
            });

        var coordinator = new NavigationCoordinator(
            navigation.Object,
            new EditSessionGuard(activeSession, confirmation.Object, settings.Object),
            new AllowAudioCaptureNavigationGuard());
        var openSelection = new OpenSelectionOverlayUseCase(
            coordinator,
            activeSession,
            TestUseCaseExecutor.Instance);
        var showMainWindow = new ShowMainWindowUseCase(coordinator, TestUseCaseExecutor.Instance);

        OpenSelectionOverlayResponse openResponse = (await openSelection.ExecuteAsync(
            new OpenSelectionOverlayRequest(CaptureOptions.ImageDefault),
            TestContext.CancellationToken)).Value!;
        ShowMainWindowResponse closeResponse = (await showMainWindow.ExecuteAsync(
            new ShowMainWindowRequest(CreateIfUnavailable: false),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(openResponse.Succeeded);
        Assert.IsTrue(closeResponse.Succeeded);
        CollectionAssert.AreEqual(
            new[]
            {
                (Route: (object)NavigationRoute.Home, ClearHistory: true),
                (Route: (object)NavigationRoute.SelectionOverlay, ClearHistory: false),
            },
            navigationOrder);
        confirmation.Verify(
            service => service.ConfirmLeaveAsync(session.Object, It.IsAny<CancellationToken>()),
            Times.Once);
        navigation.Verify(
            service => service.TryGoBackToAsync(
                It.IsAny<Func<INavigationRequest, bool>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task StopVideoCaptureUseCase_StopsCaptureAndNavigatesToVideoEdit()
    {
        var pendingVideo = new PendingVideoFile("capture.mp4");
        var videoCapture = new FakeVideoCaptureWorkflow { PendingVideo = pendingVideo };
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        var useCase = new StopVideoCaptureUseCase(navigation.Object, videoCapture, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new StopVideoCaptureRequest()));
        StopVideoCaptureResponse response = (await useCase.ExecuteAsync(new StopVideoCaptureRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.AreEqual(1, videoCapture.StopCallCount);
        navigation.Verify(
            service => service.NavigateAsync(
                NavigationRoute.VideoEdit,
                It.Is<OpenVideoEditPageRequest>(request => request.VideoFile == pendingVideo),
                false,
                TestContext.CancellationToken),
            Times.Once);
    }

    [TestMethod]
    public async Task StopVideoCaptureUseCase_WhenHostRejects_ReportsFailure()
    {
        var pendingVideo = new PendingVideoFile("capture.mp4");
        var videoCapture = new FakeVideoCaptureWorkflow { PendingVideo = pendingVideo };
        var navigation = new Mock<INavigationService>();
        navigation.SetupGet(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        navigation
            .Setup(service => service.NavigateAsync(
                NavigationRoute.VideoEdit,
                pendingVideo,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.Rejected);
        var useCase = new StopVideoCaptureUseCase(navigation.Object, videoCapture, TestUseCaseExecutor.Instance);

        StopVideoCaptureResponse response = (await useCase.ExecuteAsync(
            new StopVideoCaptureRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        Assert.AreEqual(1, videoCapture.StopCallCount);
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
    public async Task CancelVideoCaptureUseCase_WhenConfirmationIsSkipped_CancelsWithoutPrompt()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        ICancelVideoCaptureUseCase useCase = CreateCancelVideoCaptureUseCase(
            videoCapture,
            shouldWarnBeforeDiscard: true,
            confirmationService: confirmationService.Object);

        CancelVideoCaptureResponse response = (await useCase.ExecuteAsync(
            new CancelVideoCaptureRequest(SkipConfirmation: true),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.AreEqual(1, videoCapture.CancelCallCount);
        Assert.AreEqual(CancelVideoCaptureReason.User, videoCapture.LastCancelReason);
        confirmationService.Verify(
            service => service.ConfirmDiscardActiveCaptureAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CancelVideoCaptureUseCase_WhenStartTimesOut_UsesFailureCancellationReasonWithoutPrompt()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        ICancelVideoCaptureUseCase useCase = CreateCancelVideoCaptureUseCase(
            videoCapture,
            shouldWarnBeforeDiscard: true,
            confirmationService: confirmationService.Object);

        CancelVideoCaptureResponse response = (await useCase.ExecuteAsync(
            new CancelVideoCaptureRequest(Reason: CancelVideoCaptureReason.StartTimeout),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.AreEqual(1, videoCapture.CancelCallCount);
        Assert.AreEqual(CancelVideoCaptureReason.StartTimeout, videoCapture.LastCancelReason);
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
        TestNavigationService.AcceptAll(navigation);
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        navigation
            .Setup(service => service.TryGoBackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.NoChange);
        ICancelVideoCaptureUseCase cancelUseCase = CreateCancelVideoCaptureUseCase(videoCapture, shouldWarnBeforeDiscard: false);
        var useCase = new GoBackFromCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase,
            TestNavigationCoordinator.Create(navigation.Object),
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new GoBackFromCaptureOverlayRequest()));
        GoBackFromCaptureOverlayResponse response = (await useCase.ExecuteAsync(new GoBackFromCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.VideoCaptureCanceled);
        Assert.AreEqual(1, videoCapture.CancelCallCount);
        navigation.Verify(
            service => service.NavigateAsync(
                NavigationRoute.SelectionOverlay,
                CaptureOptions.VideoDefault,
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
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
        TestNavigationService.AcceptAll(navigation);
        navigation
            .Setup(service => service.TryGoBackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.Accepted);
        var useCase = new GoBackFromCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase.Object,
            TestNavigationCoordinator.Create(navigation.Object),
            TestUseCaseExecutor.Instance);

        GoBackFromCaptureOverlayResponse response = (await useCase.ExecuteAsync(new GoBackFromCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.VideoCaptureCanceled);
        navigation.Verify(service => service.TryGoBackAsync(It.IsAny<CancellationToken>()), Times.Never);
        navigation.Verify(
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
        TestNavigationService.AcceptAll(navigation);
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        navigation
            .Setup(service => service.TryGoBackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.Accepted);
        var useCase = new GoBackFromCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase,
            TestNavigationCoordinator.Create(navigation.Object),
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new GoBackFromCaptureOverlayRequest()));
        GoBackFromCaptureOverlayResponse response = (await useCase.ExecuteAsync(new GoBackFromCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.VideoCaptureCanceled);
        Assert.AreEqual(0, videoCapture.CancelCallCount);
        confirmationService.Verify(
            service => service.ConfirmDiscardActiveCaptureAsync(TestContext.CancellationToken),
            Times.Once);
        navigation.Verify(service => service.TryGoBackAsync(It.IsAny<CancellationToken>()), Times.Never);
        navigation.Verify(
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CloseCaptureOverlayUseCase_CancelsCaptureAndShowsMainWindow()
    {
        var videoCapture = new FakeVideoCaptureWorkflow { IsRecording = true };
        ICancelVideoCaptureUseCase cancelUseCase = CreateCancelVideoCaptureUseCase(videoCapture, shouldWarnBeforeDiscard: false);
        var showMainWindow = new Mock<IShowMainWindowUseCase>();
        var shutdownHandler = new Mock<IShutdownHandler>();
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        showMainWindow
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<ShowMainWindowRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<ShowMainWindowResponse>.Success(new ShowMainWindowResponse()));
        var useCase = new CloseCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase,
            showMainWindow.Object,
            shutdownHandler.Object,
            navigation.Object,
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new CloseCaptureOverlayRequest()));
        CloseCaptureOverlayResponse response = (await useCase.ExecuteAsync(new CloseCaptureOverlayRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.VideoCaptureCanceled);
        Assert.AreEqual(1, videoCapture.CancelCallCount);
        showMainWindow.Verify(
            useCase => useCase.ExecuteAsync(
                It.Is<ShowMainWindowRequest>(request => !request.CreateIfUnavailable),
                TestContext.CancellationToken),
            Times.Once);
        shutdownHandler.Verify(handler => handler.Shutdown(), Times.Never);
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
        var shutdownHandler = new Mock<IShutdownHandler>();
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        var useCase = new CloseCaptureOverlayUseCase(
            videoCapture,
            cancelUseCase,
            showMainWindow.Object,
            shutdownHandler.Object,
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
        shutdownHandler.Verify(handler => handler.Shutdown(), Times.Never);
    }

    [TestMethod]
    public async Task CloseCaptureOverlayUseCase_WhenNoMainWindowExists_ShutsDown()
    {
        var videoCapture = new FakeVideoCaptureWorkflow();
        var showMainWindow = new Mock<IShowMainWindowUseCase>();
        showMainWindow
            .Setup(useCase => useCase.ExecuteAsync(
                It.Is<ShowMainWindowRequest>(request => !request.CreateIfUnavailable),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<ShowMainWindowResponse>.Success(new ShowMainWindowResponse(false)));
        var shutdownHandler = new Mock<IShutdownHandler>();
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation.Setup(service => service.CurrentRequest).Returns(CreateNavigationRequest(NavigationRoute.CaptureOverlay));
        var useCase = new CloseCaptureOverlayUseCase(
            videoCapture,
            Mock.Of<ICancelVideoCaptureUseCase>(),
            showMainWindow.Object,
            shutdownHandler.Object,
            navigation.Object,
            TestUseCaseExecutor.Instance);

        CloseCaptureOverlayResponse response = (await useCase.ExecuteAsync(
            new CloseCaptureOverlayRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.VideoCaptureCanceled);
        showMainWindow.VerifyAll();
        shutdownHandler.Verify(handler => handler.Shutdown(), Times.Once);
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
        TestNavigationService.AcceptAll(navigation);
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation
            .Setup(service => service.TryGoBackToAsync(
                It.IsAny<Func<INavigationRequest, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.NoChange);
        var useCase = new ShowMainWindowUseCase(
            TestNavigationCoordinator.Create(navigation.Object),
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new ShowMainWindowRequest()));
        ShowMainWindowResponse response = (await useCase.ExecuteAsync(new ShowMainWindowRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        navigation.Verify(
            service => service.NavigateAsync(NavigationRoute.Home, null, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ShowMainWindowUseCase_WhenCreationIsDisabledAndNoMainWindowExists_DoesNotNavigate()
    {
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation.Setup(service => service.CanGoBack).Returns(true);
        navigation
            .Setup(service => service.TryGoBackToAsync(
                It.IsAny<Func<INavigationRequest, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.NoChange);
        var useCase = new ShowMainWindowUseCase(
            TestNavigationCoordinator.Create(navigation.Object),
            TestUseCaseExecutor.Instance);

        ShowMainWindowResponse response = (await useCase.ExecuteAsync(
            new ShowMainWindowRequest(CreateIfUnavailable: false),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        navigation.Verify(
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
