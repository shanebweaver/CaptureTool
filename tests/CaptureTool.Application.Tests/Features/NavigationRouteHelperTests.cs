using CaptureTool.Application.Abstractions.Features.Navigation;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class NavigationRouteHelperTests
{
    [TestMethod]
    public void IsMainWindowRoute_ReturnsTrue_ForMainWindowRoutes()
    {
        NavigationRoute[] routes = [
            NavigationRoute.Loading,
            NavigationRoute.Error,
            NavigationRoute.Home,
            NavigationRoute.Settings,
            NavigationRoute.About,
            NavigationRoute.Store,
            NavigationRoute.AudioCapture,
            NavigationRoute.ImageEdit,
            NavigationRoute.VideoEdit,
            NavigationRoute.AudioEdit,
        ];

        foreach (NavigationRoute route in routes)
        {
            Assert.IsTrue(CaptureToolNavigationRouteHelper.IsMainWindowRoute(route), route.ToString());
        }
    }

    [TestMethod]
    public void IsMainWindowRoute_ReturnsFalse_ForOverlayRoutesAndUnknownValues()
    {
        Assert.IsFalse(CaptureToolNavigationRouteHelper.IsMainWindowRoute(NavigationRoute.SelectionOverlay));
        Assert.IsFalse(CaptureToolNavigationRouteHelper.IsMainWindowRoute(NavigationRoute.CaptureOverlay));
        Assert.IsFalse(CaptureToolNavigationRouteHelper.IsMainWindowRoute("Home"));
    }

    [TestMethod]
    public void IsOverlayRoute_ReturnsTrue_ForOverlayRoutes()
    {
        Assert.IsTrue(CaptureToolNavigationRouteHelper.IsOverlayRoute(NavigationRoute.SelectionOverlay));
        Assert.IsTrue(CaptureToolNavigationRouteHelper.IsOverlayRoute(NavigationRoute.CaptureOverlay));
    }

    [TestMethod]
    public void IsOverlayRoute_ReturnsFalse_ForMainWindowRoutesAndUnknownValues()
    {
        Assert.IsFalse(CaptureToolNavigationRouteHelper.IsOverlayRoute(NavigationRoute.Home));
        Assert.IsFalse(CaptureToolNavigationRouteHelper.IsOverlayRoute("CaptureOverlay"));
    }
}
