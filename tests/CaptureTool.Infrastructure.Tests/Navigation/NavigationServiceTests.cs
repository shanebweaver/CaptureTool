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
    public void Navigate_PushesNewRequest()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);

        Assert.IsNotNull(service.CurrentRequest);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.HasCount(1, handler.HandledRequests);
    }

    [TestMethod]
    public void Navigate_DoesNotNavigate_WhenRequestIsSame()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Home);

        Assert.HasCount(1, handler.HandledRequests);
    }

    [TestMethod]
    public void Navigate_ClearsHistory_WhenRequested()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Settings);

        service.Navigate(TestRoute.About, null, clearHistory: true);

        Assert.IsFalse(service.CanGoBack);
        Assert.AreEqual(TestRoute.About, service.CurrentRequest?.Route);
    }

    [TestMethod]
    public void TryGoBack_GoesToPreviousRequest()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Settings);

        bool result = service.TryGoBack();

        Assert.IsTrue(result);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.HasCount(3, handler.HandledRequests);
    }

    [TestMethod]
    public void TryGoBack_Throws_WhenNoPrevious()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);

        bool result = service.TryGoBack();

        Assert.IsFalse(result);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
        Assert.HasCount(1, handler.HandledRequests);
    }

    [TestMethod]
    public void TryGoBack_DoesNothing_WhenBackTargetMatchesCurrent()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Home);

        Assert.HasCount(1, handler.HandledRequests);
        Assert.IsFalse(service.CanGoBack);
    }

    [TestMethod]
    public void TryGoBackTo_SkipsWhilePredicateTrue()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Settings);
        service.Navigate(TestRoute.About);

        bool result = service.TryGoBackTo(
            request => request.Route is TestRoute testRoute && testRoute == TestRoute.Home);

        Assert.IsTrue(result);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
    }

    [TestMethod]
    public void TryGoBackTo_ReturnsFalse_WhenPredicateDoesntSkip()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Settings);

        bool result = service.TryGoBackTo(request => false);

        Assert.IsFalse(result);
        Assert.AreEqual(TestRoute.Settings, service.CurrentRequest?.Route);
    }

    [TestMethod]
    public void TryGoBackTo_Throws_WhenNoHistory()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);

        bool result = service.TryGoBackTo(_ => true);

        Assert.IsFalse(result);
        Assert.AreEqual(TestRoute.Home, service.CurrentRequest?.Route);
    }

    [TestMethod]
    public void Navigated_Event_IsRaised()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        INavigationRequest? receivedRequest = null;

        service.Navigated += (sender, args) =>
        {
            receivedRequest = args.Request;
        };

        service.Navigate(TestRoute.Home);

        Assert.IsNotNull(receivedRequest);
        Assert.AreEqual(TestRoute.Home, receivedRequest?.Route);
    }

    [TestMethod]
    public void Navigate_TracksSafeRouteAndParameterType()
    {
        var telemetry = new RecordingTelemetryService();
        var service = new NavigationService(telemetry);
        service.SetNavigationHandler(new MockNavigationHandler());

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Settings, new NavigationParameter("private-value"));

        var trackedEvent = telemetry.Events.Last();
        Assert.AreEqual(TelemetryEvents.NavigationCompleted, trackedEvent.Name);
        Assert.AreEqual(nameof(TestRoute.Home), trackedEvent.Properties[TelemetryProperties.FromRoute]);
        Assert.AreEqual(nameof(TestRoute.Settings), trackedEvent.Properties[TelemetryProperties.ToRoute]);
        Assert.AreEqual(nameof(NavigationParameter), trackedEvent.Properties[TelemetryProperties.ParameterType]);
        Assert.IsFalse(trackedEvent.Properties.Values.Contains("private-value"));
    }

    [TestMethod]
    public void Navigate_Throws_WhenNoHandlerSet()
    {
        var service = new NavigationService();

        Assert.ThrowsExactly<InvalidOperationException>(() => service.Navigate(TestRoute.Home));
    }

    [TestMethod]
    public void CurrentRequest_IsNull_WhenNoNavigation()
    {
        var service = new NavigationService();

        Assert.IsNull(service.CurrentRequest);
        Assert.IsFalse(service.CanGoBack);
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
