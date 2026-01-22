using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.CaptureOverlay;
using CaptureTool.Application.Implementations.Services.Navigation;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.Navigation;
using CaptureTool.Infrastructure.Interfaces.Navigation;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.CaptureOverlay;

[TestClass]
public class CaptureOverlayGoBackUseCaseTests
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
        var handler = Fixture.Create<CaptureOverlayGoBackUseCase>();

        bool can = handler.CanExecute();
        Assert.IsFalse(can);
    }

    [TestMethod]
    public void CanExecute_ShouldBeFalse_WhenNotVideoCaptureRoute()
    {
        var navService = Fixture.Freeze<Mock<INavigationService>>();
        navService.SetupGet(n => n.CanGoBack).Returns(true);
        navService.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(CaptureToolNavigationRoute.Home));
        var handler = Fixture.Create<CaptureOverlayGoBackUseCase>();

        bool can = handler.CanExecute();
        Assert.IsFalse(can);
    }

    [TestMethod]
    public void CanExecute_ShouldBeTrue_WhenOnVideoCaptureAndCanGoBack()
    {
        var navService = Fixture.Freeze<Mock<INavigationService>>();
        navService.SetupGet(n => n.CanGoBack).Returns(true);
        navService.SetupGet(n => n.CurrentRequest).Returns(new NavigationRequest(CaptureToolNavigationRoute.VideoCapture));
        var handler = Fixture.Create<CaptureOverlayGoBackUseCase>();

        bool can = handler.CanExecute();
        Assert.IsTrue(can);
    }

    [TestMethod]
    public void Execute_ShouldCancelRecording_AndGoBackOrToImageCapture()
    {
        var video = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();

        appNav.Setup(a => a.TryGoBack()).Returns(true);

        var handler = Fixture.Create<CaptureOverlayGoBackUseCase>();

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

        var handler = Fixture.Create<CaptureOverlayGoBackUseCase>();

        handler.Execute();

        video.Verify(v => v.CancelVideoCapture(), Times.Once);
        appNav.Verify(a => a.TryGoBack(), Times.Once);
        appNav.Verify(a => a.GoToImageCapture(CaptureOptions.VideoDefault, true), Times.Once);
    }
}
