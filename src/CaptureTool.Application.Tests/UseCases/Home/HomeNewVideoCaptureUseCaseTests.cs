using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.UseCases.Home;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.Home;

[TestClass]
public class HomeNewVideoCaptureUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldNavigateToVideoCapture()
    {
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();

        var action = Fixture.Create<HomeNewVideoCaptureUseCase>();
        action.Execute();
        appNav.Verify(a => a.GoToImageCapture(CaptureOptions.VideoDefault), Times.Once);
    }
}
