using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Navigation;
using Moq;

namespace CaptureTool.Application.Tests.Navigation;

[TestClass]
public sealed class NavigationCoordinatorTests
{
    [TestMethod]
    public async Task NavigateAsync_WhenGuardsAccept_DispatchesNavigation()
    {
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var editGuard = CreateGuard<IEditSessionGuard>(
            guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()));
        var audioGuard = CreateGuard<IAudioCaptureNavigationGuard>(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()));
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard.Object, audioGuard.Object);

        bool navigated = await coordinator.NavigateAsync(
            NavigationRoute.Home,
            clearHistory: true,
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(navigated);
        navigation.Verify(
            service => service.NavigateAsync(NavigationRoute.Home, null, true, TestContext.CancellationToken),
            Times.Once);
        editGuard.Verify(
            guard => guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken),
            Times.Once);
        audioGuard.Verify(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken),
            Times.Once);
    }

    [TestMethod]
    public async Task NavigateAsync_WhenEditGuardRejects_DoesNotEvaluateAudioOrNavigate()
    {
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var editGuard = new Mock<IEditSessionGuard>();
        editGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var audioGuard = new Mock<IAudioCaptureNavigationGuard>();
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard.Object, audioGuard.Object);

        bool navigated = await coordinator.NavigateAsync(
            NavigationRoute.SelectionOverlay,
            cancellationToken: TestContext.CancellationToken);

        Assert.IsFalse(navigated);
        audioGuard.Verify(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        navigation.Verify(
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task NavigateAsync_WhenAudioGuardRejects_DoesNotNavigate()
    {
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var editGuard = CreateGuard<IEditSessionGuard>(
            guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()));
        var audioGuard = new Mock<IAudioCaptureNavigationGuard>();
        audioGuard
            .Setup(guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard.Object, audioGuard.Object);

        bool navigated = await coordinator.NavigateAsync(
            NavigationRoute.Store,
            cancellationToken: TestContext.CancellationToken);

        Assert.IsFalse(navigated);
        navigation.Verify(
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task NavigateAsync_WhenHostRejects_ReturnsFalse()
    {
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation
            .Setup(service => service.NavigateAsync(
                NavigationRoute.Store,
                null,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.Rejected);
        var editGuard = CreateGuard<IEditSessionGuard>(
            guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()));
        var audioGuard = CreateGuard<IAudioCaptureNavigationGuard>(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()));
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard.Object, audioGuard.Object);

        bool navigated = await coordinator.NavigateAsync(
            NavigationRoute.Store,
            cancellationToken: TestContext.CancellationToken);

        Assert.IsFalse(navigated);
        editGuard.Verify(
            guard => guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken),
            Times.Once);
        audioGuard.Verify(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken),
            Times.Once);
    }

    [TestMethod]
    public async Task NavigateAsync_ForExactCurrentRequest_DoesNotPromptOrDispatch()
    {
        var currentRequest = new Mock<INavigationRequest>();
        currentRequest.SetupGet(request => request.Route).Returns(NavigationRoute.ImageEdit);
        currentRequest.SetupGet(request => request.Parameter).Returns("capture.png");
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation.SetupGet(service => service.CurrentRequest).Returns(currentRequest.Object);
        var editGuard = new Mock<IEditSessionGuard>();
        var audioGuard = new Mock<IAudioCaptureNavigationGuard>();
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard.Object, audioGuard.Object);

        bool navigated = await coordinator.NavigateAsync(
            NavigationRoute.ImageEdit,
            "capture.png",
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(navigated);
        editGuard.Verify(
            guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        navigation.Verify(
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ExecuteTransitionAsync_NestedNavigation_EvaluatesLeavePolicyOnce()
    {
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var editGuard = CreateGuard<IEditSessionGuard>(
            guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()));
        var audioGuard = CreateGuard<IAudioCaptureNavigationGuard>(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()));
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard.Object, audioGuard.Object);

        bool navigated = await coordinator.ExecuteTransitionAsync(
            token => coordinator.NavigateAsync(NavigationRoute.VideoEdit, cancellationToken: token),
            TestContext.CancellationToken);

        Assert.IsTrue(navigated);
        editGuard.Verify(
            guard => guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken),
            Times.Once);
        audioGuard.Verify(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken),
            Times.Once);
        navigation.Verify(
            service => service.NavigateAsync(NavigationRoute.VideoEdit, null, false, TestContext.CancellationToken),
            Times.Once);
    }

    [TestMethod]
    public async Task NavigateAsync_ConcurrentRequests_SerializesGuardEvaluation()
    {
        var firstGuardEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstGuard = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int editGuardCalls = 0;
        var editGuard = new Mock<IEditSessionGuard>();
        editGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                if (Interlocked.Increment(ref editGuardCalls) == 1)
                {
                    firstGuardEntered.SetResult();
                    await releaseFirstGuard.Task;
                }

                return true;
            });
        var audioGuard = CreateGuard<IAudioCaptureNavigationGuard>(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()));
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard.Object, audioGuard.Object);

        Task<bool> first = coordinator.NavigateAsync(NavigationRoute.Home);
        await firstGuardEntered.Task;
        Task<bool> second = coordinator.NavigateAsync(NavigationRoute.Store);
        await Task.Yield();

        Assert.AreEqual(1, Volatile.Read(ref editGuardCalls));

        releaseFirstGuard.SetResult();
        Assert.IsTrue(await first);
        Assert.IsTrue(await second);
        Assert.AreEqual(2, editGuardCalls);
    }

    [TestMethod]
    public async Task NavigateAsync_AfterGuardException_ReleasesTransitionGate()
    {
        var editGuard = new Mock<IEditSessionGuard>();
        editGuard
            .SetupSequence(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("guard failed"))
            .ReturnsAsync(true);
        var audioGuard = CreateGuard<IAudioCaptureNavigationGuard>(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()));
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard.Object, audioGuard.Object);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => coordinator.NavigateAsync(NavigationRoute.Home));

        Assert.IsTrue(await coordinator.NavigateAsync(NavigationRoute.Store));
        navigation.Verify(
            service => service.NavigateAsync(NavigationRoute.Store, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task NavigateAsync_AfterGuardCancellation_ReleasesTransitionGate()
    {
        int editGuardCalls = 0;
        var editGuard = new Mock<IEditSessionGuard>();
        editGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Interlocked.Increment(ref editGuardCalls) == 1
                ? Task.FromCanceled<bool>(new CancellationToken(canceled: true))
                : Task.FromResult(true));
        var audioGuard = CreateGuard<IAudioCaptureNavigationGuard>(
            guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()));
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard.Object, audioGuard.Object);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => coordinator.NavigateAsync(NavigationRoute.Home));

        Assert.IsTrue(await coordinator.NavigateAsync(NavigationRoute.Store));
        navigation.Verify(
            service => service.NavigateAsync(NavigationRoute.Store, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<TGuard> CreateGuard<TGuard>(System.Linq.Expressions.Expression<Func<TGuard, Task<bool>>> expression)
        where TGuard : class
    {
        var guard = new Mock<TGuard>();
        guard.Setup(expression).ReturnsAsync(true);
        return guard;
    }

    public TestContext TestContext { get; set; } = null!;
}
