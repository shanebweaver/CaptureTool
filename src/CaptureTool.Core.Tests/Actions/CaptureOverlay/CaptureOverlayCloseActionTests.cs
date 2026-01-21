using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.CaptureOverlay;
using CaptureTool.Core.Implementations.Services.Navigation;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.Navigation;
using CaptureTool.Infrastructure.Interfaces.Navigation;
using Moq;

namespace CaptureTool.Core.Tests.Actions.CaptureOverlay;

[TestClass]
public class CaptureOverlayCloseActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void CanExecute_ShouldBeFalse_WhenCannotGoBack()
    {
        var navService = Fixture.Freeze<Mock<INavigationService>>();
        navService.SetupGet(n => n.CanGoBack).Returns(false);
        var handler = Fixture.Create<CaptureOverlayCloseAction>();

        bool can = handler.CanExecute();
        Assert.IsFalse(can);
    }

    [TestMethod]
    public void CanExecute_ShouldBeFalse_WhenNotVideoCaptureRoute()
    {
        var navService = Fixture.Freeze<Mock<INavigationService>>();
        navService.SetupGet(n => n.CanGoBack).Returns(true);
        navService.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(CaptureToolNavigationRoute.Home));
        var handler = Fixture.Create<CaptureOverlayCloseAction>();

        bool can = handler.CanExecute();
        Assert.IsFalse(can);
    }

    [TestMethod]
    public void CanExecute_ShouldBeTrue_WhenOnVideoCaptureAndCanGoBack()
    {
        var navService = Fixture.Freeze<Mock<INavigationService>>();
        navService.SetupGet(n => n.CanGoBack).Returns(true);
        navService.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(CaptureToolNavigationRoute.VideoCapture));
        var handler = Fixture.Create<CaptureOverlayCloseAction>();

        bool can = handler.CanExecute();
        Assert.IsTrue(can);
    }

    [TestMethod]
    public void Execute_ShouldCancelRecording_AndGoBackOrShutdown()
    {
        var video = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();
        var shutdown = Fixture.Freeze<Mock<Infrastructure.Interfaces.Shutdown.IShutdownHandler>>();

        appNav.SetupGet(a => a.CanGoBack).Returns(true);

        var handler = Fixture.Create<CaptureOverlayCloseAction>();

        handler.Execute();

        video.Verify(v => v.CancelVideoCapture(), Times.Once);
        appNav.Verify(a => a.GoBackToMainWindow(), Times.Once);
        shutdown.Verify(s => s.Shutdown(), Times.Never);
    }

    [TestMethod]
    public void Execute_ShouldShutdown_WhenCannotGoBack()
    {
        var video = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();
        var shutdown = Fixture.Freeze<Mock<Infrastructure.Interfaces.Shutdown.IShutdownHandler>>();

        appNav.SetupGet(a => a.CanGoBack).Returns(false);

        var handler = Fixture.Create<CaptureOverlayCloseAction>();

        handler.Execute();

        video.Verify(v => v.CancelVideoCapture(), Times.Once);
        appNav.Verify(a => a.GoBackToMainWindow(), Times.Never);
        shutdown.Verify(s => s.Shutdown(), Times.Once);
    }
}
