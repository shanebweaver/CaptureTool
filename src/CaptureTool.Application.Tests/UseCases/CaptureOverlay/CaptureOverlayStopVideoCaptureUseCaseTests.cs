using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.UseCases.CaptureOverlay;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using Moq;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.UseCases.Navigation;

namespace CaptureTool.Application.Tests.UseCases.CaptureOverlay;

[TestClass]
public class CaptureOverlayStopVideoCaptureUseCaseTests
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
        nav.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(NavigationRoute.Home));

        var action = Fixture.Create<CaptureOverlayStopVideoCaptureUseCase>();
        bool can = action.CanExecute();
        Assert.IsFalse(can);
    }

    [TestMethod]
    public void CanExecute_ShouldBeTrue_OnVideoCaptureRoute()
    {
        var nav = Fixture.Freeze<Mock<INavigationService>>();
        nav.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(NavigationRoute.CaptureOverlay));

        var action = Fixture.Create<CaptureOverlayStopVideoCaptureUseCase>();
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

        var action = Fixture.Create<CaptureOverlayStopVideoCaptureUseCase>();
        action.Execute();

        video.Verify(v => v.StopVideoCapture(), Times.Once);
        appNav.Verify(a => a.GoToVideoEdit(pendingFile), Times.Once);
    }
}
