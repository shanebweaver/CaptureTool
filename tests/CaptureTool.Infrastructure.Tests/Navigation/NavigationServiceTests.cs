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
    public void Navigate_TracksNavigationTelemetry()
    {
        var telemetry = new RecordingTelemetry();
        var telemetryContext = new RecordingTelemetryContext();
        var service = new NavigationService(telemetry, telemetryContext);
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Settings, parameter: "from-home");

        Assert.AreEqual("Settings", telemetryContext.CurrentRoute);
        Assert.HasCount(2, telemetry.Events);
        Assert.AreEqual(TelemetryEvents.NavigationCompleted, telemetry.Events[1].EventName);
        Assert.AreEqual("Home", telemetry.Events[1].Attributes[TelemetryAttributes.FromRoute]);
        Assert.AreEqual("Settings", telemetry.Events[1].Attributes[TelemetryAttributes.ToRoute]);
        Assert.AreEqual("String", telemetry.Events[1].Attributes[TelemetryAttributes.ParameterType]);
        Assert.HasCount(2, telemetry.Metrics);
        Assert.AreEqual("navigation.completed", telemetry.Metrics[1].MetricName);
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
}
