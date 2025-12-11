using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Home;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using Moq;

namespace CaptureTool.Core.Tests.Home;

[TestClass]
public class HomeNewImageCaptureActionTests
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
        var action = Fixture.Create<HomeNewImageCaptureAction>();
        action.Execute();
        appNav.Verify(a => a.GoToImageCapture(CaptureOptions.ImageDefault), Times.Once);
    }
}
