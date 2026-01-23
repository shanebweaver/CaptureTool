using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.Home;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.Home;

[TestClass]
public class HomeNewImageCaptureUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldNavigateToImageCapture()
    {
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();
        var action = Fixture.Create<HomeNewImageCaptureUseCase>();
        action.Execute();
        appNav.Verify(a => a.GoToImageCapture(CaptureOptions.ImageDefault), Times.Once);
    }
}
