using CaptureTool.Services.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CaptureTool.Services.Tests;

[TestClass]
public sealed class NavigationTests
{
    private NavigationService _navigationService = null!;

    [TestInitialize]
    public void Setup()
    {
        _navigationService = new();
    }

    [TestMethod]
    public void NavigationTest()
    {
        INavigationHandler navigationHandler = new MockNavigationHandler();
        _navigationService.SetNavigationHandler(navigationHandler);
        Assert.IsFalse(_navigationService.CanGoBack);

        NavigationRoute route1 = new("test_route_1");
        object? parameter1 = null;
        _navigationService.Navigate(route1, parameter1);
        Assert.AreEqual(_navigationService.CurrentRequest?.Route, route1);
        Assert.IsFalse(_navigationService.CanGoBack);

        NavigationRoute route2 = new("test_route_2");
        object? parameter2 = null;
        _navigationService.Navigate(route2, parameter2);
        Assert.AreEqual(_navigationService.CurrentRequest?.Route, route2);
        Assert.IsTrue(_navigationService.CanGoBack);

        _navigationService.GoBack();
        Assert.IsFalse(_navigationService.CanGoBack);
        Assert.AreEqual(_navigationService.CurrentRequest?.Route, route1);

        NavigationRoute route3 = new("test_route_3");
        object? parameter3 = null;
        _navigationService.Navigate(route3, parameter3, true);
        Assert.IsFalse(_navigationService.CanGoBack);
        Assert.AreEqual(_navigationService.CurrentRequest?.Route, route3);
    }
}
