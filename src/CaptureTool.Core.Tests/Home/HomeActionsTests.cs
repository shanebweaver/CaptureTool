using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Home;
using CaptureTool.Core.Interfaces.Actions.Home;
using Moq;

namespace CaptureTool.Core.Tests.Home;

[TestClass]
public class HomeActionsTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void NewImageCapture_ShouldDelegateToAction()
    {
        var imageAction = Fixture.Freeze<Mock<IHomeNewImageCaptureAction>>();
        var videoAction = Fixture.Freeze<Mock<IHomeNewVideoCaptureAction>>();
        imageAction.Setup(a => a.CanExecute()).Returns(true);

        var actions = new HomeActions(imageAction.Object, videoAction.Object);
        actions.NewImageCapture();

        imageAction.Verify(a => a.CanExecute(), Times.Once);
        imageAction.Verify(a => a.Execute(), Times.Once);
    }

    [TestMethod]
    public void NewVideoCapture_ShouldDelegateToAction()
    {
        var imageAction = Fixture.Freeze<Mock<IHomeNewImageCaptureAction>>();
        var videoAction = Fixture.Freeze<Mock<IHomeNewVideoCaptureAction>>();
        videoAction.Setup(a => a.CanExecute()).Returns(true);

        var actions = new HomeActions(imageAction.Object, videoAction.Object);
        actions.NewVideoCapture();

        videoAction.Verify(a => a.CanExecute(), Times.Once);
        videoAction.Verify(a => a.Execute(), Times.Once);
    }
}
