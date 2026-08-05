using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Navigation;

namespace CaptureTool.Infrastructure.Tests.Navigation;

[TestClass]
public class NavigationServiceTests
{
    private enum TestRoute
    {
        Home,
        Settings,
        About,
    }

    [TestMethod]
    public async Task NavigateAsync_AfterHostAcceptance_CommitsRequest()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        NavigationResult result = await service.NavigateAsync(TestRoute.Home);

        Assert.AreEqual(NavigationResult.Accepted, result);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.HasCount(1, handler.HandledRequests);
    }

    [TestMethod]
    public async Task NavigateAsync_WhileHostAcceptanceIsPending_DoesNotCommitRequest()
    {
        var acceptance = new TaskCompletionSource<NavigationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MockNavigationHandler
        {
            HandleAsync = (_, _) => acceptance.Task
        };
        var service = new NavigationService();
        service.SetNavigationHandler(handler);

        Task<NavigationResult> navigation = service.NavigateAsync(TestRoute.Home);
        await WaitForAsync(() => handler.HandledRequests.Count == 1);
        Task<NavigationResult> queuedNavigation = service.NavigateAsync(TestRoute.Settings);
        await Task.Delay(20);

        Assert.IsNull(service.CurrentRequest);
        Assert.HasCount(1, handler.HandledRequests);

        acceptance.SetResult(NavigationResult.Accepted);
        Assert.AreEqual(NavigationResult.Accepted, await navigation);
        Assert.AreEqual(NavigationResult.Accepted, await queuedNavigation);
        Assert.AreEqual(TestRoute.Settings, service.CurrentRequest?.Route);
    }

    [TestMethod]
    public async Task NavigateAsync_WhenHostRejects_RetainsStateAndEmitsNoCompletionSignals()
    {
        var telemetry = new RecordingTelemetryService();
        var handler = new MockNavigationHandler();
        var service = new NavigationService(telemetry);
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);
        int navigatedCount = 0;
        service.Navigated += (_, _) => navigatedCount++;
        telemetry.Events.Clear();
        handler.Result = NavigationResult.Rejected;

        NavigationResult result = await service.NavigateAsync(TestRoute.Settings);

        Assert.AreEqual(NavigationResult.Rejected, result);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.IsFalse(service.CanGoBack);
        Assert.AreEqual(0, navigatedCount);
        Assert.IsEmpty(telemetry.Events);
    }

    [TestMethod]
    public async Task NavigateAsync_WhenHandlerThrows_RetainsStateAndReleasesGate()
    {
        int calls = 0;
        var handler = new MockNavigationHandler
        {
            HandleAsync = (_, _) => Interlocked.Increment(ref calls) == 2
                ? Task.FromException<NavigationResult>(new InvalidOperationException("host failed"))
                : Task.FromResult(NavigationResult.Accepted)
        };
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.NavigateAsync(TestRoute.Settings));

        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.AreEqual(NavigationResult.Accepted, await service.NavigateAsync(TestRoute.About));
        Assert.AreEqual(TestRoute.About, service.CurrentRequest?.Route);
    }

    [TestMethod]
    public async Task NavigateAsync_WhenHandlerIsCanceled_RetainsStateAndReleasesGate()
    {
        int calls = 0;
        var handler = new MockNavigationHandler
        {
            HandleAsync = (_, _) => Interlocked.Increment(ref calls) == 2
                ? Task.FromCanceled<NavigationResult>(new CancellationToken(canceled: true))
                : Task.FromResult(NavigationResult.Accepted)
        };
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => service.NavigateAsync(TestRoute.Settings));

        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.AreEqual(NavigationResult.Accepted, await service.NavigateAsync(TestRoute.About));
    }

    [TestMethod]
    public async Task NavigateAsync_ForExactRequest_ReturnsNoChangeWithoutDispatch()
    {
        var handler = new MockNavigationHandler();
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);

        NavigationResult result = await service.NavigateAsync(TestRoute.Home);

        Assert.AreEqual(NavigationResult.NoChange, result);
        Assert.HasCount(1, handler.HandledRequests);
    }

    [TestMethod]
    public async Task NavigateAsync_WithClearHistory_CommitsOnlyAfterAcceptance()
    {
        var handler = new MockNavigationHandler();
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);
        await service.NavigateAsync(TestRoute.Settings);

        NavigationResult result = await service.NavigateAsync(TestRoute.About, clearHistory: true);

        Assert.AreEqual(NavigationResult.Accepted, result);
        Assert.IsFalse(service.CanGoBack);
        Assert.AreEqual(TestRoute.About, service.CurrentRequest?.Route);
    }

    [TestMethod]
    public async Task TryGoBackAsync_WhenAccepted_CommitsPreviousRequest()
    {
        var handler = new MockNavigationHandler();
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);
        await service.NavigateAsync(TestRoute.Settings);

        NavigationResult result = await service.TryGoBackAsync();

        Assert.AreEqual(NavigationResult.Accepted, result);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.HasCount(3, handler.HandledRequests);
        Assert.IsTrue(handler.HandledRequests.Last().IsBackNavigation);
    }

    [TestMethod]
    public async Task TryGoBackAsync_WhenRejected_RetainsOriginalStack()
    {
        var handler = new MockNavigationHandler();
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);
        await service.NavigateAsync(TestRoute.Settings);
        handler.Result = NavigationResult.Rejected;

        NavigationResult result = await service.TryGoBackAsync();

        Assert.AreEqual(NavigationResult.Rejected, result);
        Assert.AreEqual(TestRoute.Settings, service.CurrentRequest?.Route);
        Assert.IsTrue(service.CanGoBack);
    }

    [TestMethod]
    public async Task TryGoBackAsync_WithoutHistory_ReturnsNoChange()
    {
        var handler = new MockNavigationHandler();
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);

        NavigationResult result = await service.TryGoBackAsync();

        Assert.AreEqual(NavigationResult.NoChange, result);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.HasCount(1, handler.HandledRequests);
    }

    [TestMethod]
    public async Task TryGoBackToAsync_WhenAccepted_CommitsTargetRequest()
    {
        var handler = new MockNavigationHandler();
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);
        await service.NavigateAsync(TestRoute.Settings);
        await service.NavigateAsync(TestRoute.About);

        NavigationResult result = await service.TryGoBackToAsync(request => Equals(request.Route, TestRoute.Home));

        Assert.AreEqual(NavigationResult.Accepted, result);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.IsFalse(service.CanGoBack);
    }

    [TestMethod]
    public async Task TryGoBackToAsync_WhenRejected_RetainsOriginalStack()
    {
        var handler = new MockNavigationHandler();
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);
        await service.NavigateAsync(TestRoute.Settings);
        await service.NavigateAsync(TestRoute.About);
        handler.Result = NavigationResult.Rejected;

        NavigationResult result = await service.TryGoBackToAsync(request => Equals(request.Route, TestRoute.Home));

        Assert.AreEqual(NavigationResult.Rejected, result);
        Assert.AreEqual(TestRoute.About, service.CurrentRequest?.Route);
        Assert.IsTrue(service.CanGoBack);
    }

    [TestMethod]
    public async Task TryGoBackToAsync_WithoutTarget_ReturnsNoChange()
    {
        var handler = new MockNavigationHandler();
        var service = new NavigationService();
        service.SetNavigationHandler(handler);
        await service.NavigateAsync(TestRoute.Home);
        await service.NavigateAsync(TestRoute.Settings);

        NavigationResult result = await service.TryGoBackToAsync(_ => false);

        Assert.AreEqual(NavigationResult.NoChange, result);
        Assert.AreEqual(TestRoute.Settings, service.CurrentRequest?.Route);
    }

    [TestMethod]
    public async Task NavigateAsync_AfterAcceptance_RaisesNavigatedAndTracksSafeMetadata()
    {
        var telemetry = new RecordingTelemetryService();
        var service = new NavigationService(telemetry);
        service.SetNavigationHandler(new MockNavigationHandler());
        await service.NavigateAsync(TestRoute.Home);
        INavigationRequest? receivedRequest = null;
        service.Navigated += (_, args) => receivedRequest = args.Request;

        await service.NavigateAsync(TestRoute.Settings, new NavigationParameter("private-value"));

        Assert.AreEqual(TestRoute.Settings, receivedRequest?.Route);
        var trackedEvent = telemetry.Events.Last();
        Assert.AreEqual(TelemetryEvents.NavigationCompleted, trackedEvent.Name);
        Assert.AreEqual(nameof(TestRoute.Home), trackedEvent.Properties[TelemetryProperties.FromRoute]);
        Assert.AreEqual(nameof(TestRoute.Settings), trackedEvent.Properties[TelemetryProperties.ToRoute]);
        Assert.AreEqual(nameof(NavigationParameter), trackedEvent.Properties[TelemetryProperties.ParameterType]);
        Assert.IsFalse(trackedEvent.Properties.Values.Contains("private-value"));
    }

    [TestMethod]
    public async Task NavigateAsync_WithoutHandler_ThrowsAndRetainsEmptyState()
    {
        var service = new NavigationService();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.NavigateAsync(TestRoute.Home));

        Assert.IsNull(service.CurrentRequest);
        Assert.IsFalse(service.CanGoBack);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.IsTrue(condition());
    }

    private sealed record NavigationParameter(string Secret);

    private sealed class RecordingTelemetryService : ITelemetryService
    {
        public List<(string Name, IReadOnlyDictionary<string, object?> Properties)> Events { get; } = [];

        public void TrackEvent(
            string eventName,
            IReadOnlyDictionary<string, object?>? properties = null)
        {
            Events.Add((eventName, properties ?? new Dictionary<string, object?>()));
        }
    }
}
