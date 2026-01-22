using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.Actions.Home;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using Moq;

namespace CaptureTool.Application.Tests.Actions.Home;

[TestClass]
public class HomeNewVideoCaptureActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void CanExecute_ShouldRespectFeatureFlag()
    {
        var fm = Fixture.Freeze<Mock<IFeatureManager>>();
        fm.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_VideoCapture)).Returns(false);
        var action = Fixture.Create<HomeNewVideoCaptureAction>();
        Assert.IsFalse(action.CanExecute());

        fm.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_VideoCapture)).Returns(true);
        Assert.IsTrue(action.CanExecute());
    }

    [TestMethod]
    public void Execute_ShouldNavigateToVideoCapture()
    {
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();
        var fm = Fixture.Freeze<Mock<IFeatureManager>>();
        fm.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_VideoCapture)).Returns(true);

        var action = Fixture.Create<HomeNewVideoCaptureAction>();
        action.Execute();
        appNav.Verify(a => a.GoToImageCapture(CaptureOptions.VideoDefault), Times.Once);
    }
}
