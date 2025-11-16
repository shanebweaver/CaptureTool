using CaptureTool.Services.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CaptureTool.Services.Tests.Navigation;

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
        Assert.AreEqual(1, handler.HandledRequests.Count);
    }

    [TestMethod]
    public void Navigate_DoesNotNavigate_WhenRequestIsSame()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Home);

        Assert.AreEqual(1, handler.HandledRequests.Count);
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
        Assert.AreEqual(3, handler.HandledRequests.Count);
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
        Assert.AreEqual(1, handler.HandledRequests.Count);
    }

    [TestMethod]
    public void TryGoBack_DoesNothing_WhenBackTargetMatchesCurrent()
    {
        var service = new NavigationService();
        var handler = new MockNavigationHandler();
        service.SetNavigationHandler(handler);

        service.Navigate(TestRoute.Home);
        service.Navigate(TestRoute.Home);

        Assert.AreEqual(1, handler.HandledRequests.Count);
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

        NavigationRequest? receivedRequest = null;

        service.Navigated += (sender, args) =>
        {
            receivedRequest = args.Request;
        };

        service.Navigate(TestRoute.Home);

        Assert.IsNotNull(receivedRequest);
        Assert.AreEqual(TestRoute.Home, receivedRequest?.Route);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Navigate_Throws_WhenNoHandlerSet()
    {
        var service = new NavigationService();

        service.Navigate(TestRoute.Home); // no handler -> error
    }

    [TestMethod]
    public void CurrentRequest_IsNull_WhenNoNavigation()
    {
        var service = new NavigationService();

        Assert.IsNull(service.CurrentRequest);
        Assert.IsFalse(service.CanGoBack);
    }
}