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
public class CaptureOverlayStopVideoCaptureActionTests
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

        var action = Fixture.Create<CaptureOverlayStopVideoCaptureAction>();
        bool can = action.CanExecute();
        Assert.IsFalse(can);
    }

    [TestMethod]
    public void CanExecute_ShouldBeTrue_OnVideoCaptureRoute()
    {
        var nav = Fixture.Freeze<Mock<INavigationService>>();
        nav.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(CaptureToolNavigationRoute.VideoCapture));

        var action = Fixture.Create<CaptureOverlayStopVideoCaptureAction>();
        bool can = action.CanExecute();
        Assert.IsTrue(can);
    }

    [TestMethod]
    public void Execute_ShouldStopRecording_AndNavigateToEdit()
    {
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();
        var video = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        var pendingFile = new PendingVideoFile("test.mp4");
        video.Setup(v => v.StopVideoCapture()).Returns(pendingFile);

        var action = Fixture.Create<CaptureOverlayStopVideoCaptureAction>();
        action.Execute();

        video.Verify(v => v.StopVideoCapture(), Times.Once);
        appNav.Verify(a => a.GoToVideoEdit(pendingFile), Times.Once);
    }
}
