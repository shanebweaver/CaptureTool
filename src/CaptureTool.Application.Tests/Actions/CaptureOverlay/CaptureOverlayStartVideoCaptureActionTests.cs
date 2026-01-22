using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.Actions.CaptureOverlay;
using CaptureTool.Application.Implementations.Services.Navigation;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.Navigation;
using CaptureTool.Infrastructure.Interfaces.Navigation;
using Moq;
using System.Drawing;

namespace CaptureTool.Application.Tests.Actions.CaptureOverlay;

[TestClass]
public class CaptureOverlayStartVideoCaptureActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void CanExecute_ShouldBeFalse_WhenNotOnVideoCaptureRoute()
    {
        var nav = Fixture.Freeze<Mock<INavigationService>>();
        nav.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(CaptureToolNavigationRoute.Home));

        var action = Fixture.Create<CaptureOverlayStartVideoCaptureAction>();
        bool can = action.CanExecute(new NewCaptureArgs(default, default));
        Assert.IsFalse(can);
    }

    [TestMethod]
    public void CanExecute_ShouldBeTrue_OnVideoCaptureRoute()
    {
        var nav = Fixture.Freeze<Mock<INavigationService>>();
        nav.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(CaptureToolNavigationRoute.VideoCapture));

        var action = Fixture.Create<CaptureOverlayStartVideoCaptureAction>();
        bool can = action.CanExecute(new NewCaptureArgs(default, new Rectangle(1,1,2,2)));
        Assert.IsTrue(can);
    }

    [TestMethod]
    public void Execute_ShouldNavigateAndStartRecording()
    {
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();
        var video = Fixture.Freeze<Mock<IVideoCaptureHandler>>();

        var monitor = new MonitorCaptureResult(IntPtr.Zero, new byte[4], 96, new Rectangle(0,0,10,10), new Rectangle(0,0,10,10), true);
        var args = new NewCaptureArgs(monitor, new Rectangle(1,1,10,10));

        var action = Fixture.Create<CaptureOverlayStartVideoCaptureAction>();
        action.Execute(args);

        appNav.Verify(a => a.GoToVideoCapture(args), Times.Once);
        video.Verify(v => v.StartVideoCapture(args), Times.Once);
    }
}
