using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.CaptureOverlay;
using CaptureTool.Core.Implementations.Navigation;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Implementations.Navigation;
using CaptureTool.Services.Interfaces.Navigation;
using Moq;

namespace CaptureTool.Core.Tests.Actions;

[TestClass]
public class CaptureOverlayGoBackActionTests
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
        var handler = Fixture.Create<CaptureOverlayGoBackAction>();

        bool can = handler.CanExecute();
        Assert.IsFalse(can);
    }

    [TestMethod]
    public void CanExecute_ShouldBeFalse_WhenNotVideoCaptureRoute()
    {
        var navService = Fixture.Freeze<Mock<INavigationService>>();
        navService.SetupGet(n => n.CanGoBack).Returns(true);
        navService.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(CaptureToolNavigationRoute.Home));
        var handler = Fixture.Create<CaptureOverlayGoBackAction>();

        bool can = handler.CanExecute();
        Assert.IsFalse(can);
    }

    [TestMethod]
    public void CanExecute_ShouldBeTrue_WhenOnVideoCaptureAndCanGoBack()
    {
        var navService = Fixture.Freeze<Mock<INavigationService>>();
        navService.SetupGet(n => n.CanGoBack).Returns(true);
        navService.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(CaptureToolNavigationRoute.VideoCapture));
        var handler = Fixture.Create<CaptureOverlayGoBackAction>();

        bool can = handler.CanExecute();
        Assert.IsTrue(can);
    }

    [TestMethod]
    public void Execute_ShouldCancelRecording_AndGoBackOrToImageCapture()
    {
        var video = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();

        appNav.Setup(a => a.TryGoBack()).Returns(true);

        var handler = Fixture.Create<CaptureOverlayGoBackAction>();

        handler.Execute();

        video.Verify(v => v.CancelVideoCapture(), Times.Once);
        appNav.Verify(a => a.TryGoBack(), Times.Once);
        appNav.Verify(a => a.GoToImageCapture(CaptureOptions.VideoDefault, true), Times.Never);
    }

    [TestMethod]
    public void Execute_ShouldNavigateToImageCapture_WhenTryGoBackFails()
    {
        var video = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();

        appNav.Setup(a => a.TryGoBack()).Returns(false);

        var handler = Fixture.Create<CaptureOverlayGoBackAction>();

        handler.Execute();

        video.Verify(v => v.CancelVideoCapture(), Times.Once);
        appNav.Verify(a => a.TryGoBack(), Times.Once);
        appNav.Verify(a => a.GoToImageCapture(CaptureOptions.VideoDefault, true), Times.Once);
    }
}
