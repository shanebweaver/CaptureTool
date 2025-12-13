using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.VideoEdit;
using CaptureTool.Core.Interfaces.Actions.VideoEdit;
using Moq;

namespace CaptureTool.Core.Tests.Actions.VideoEdit;

[TestClass]
public class VideoEditActionsTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public async Task SaveAsync_ShouldDelegateToAction()
    {
        var save = Fixture.Freeze<Mock<IVideoEditSaveAction>>();

        var actions = new VideoEditActions(save.Object, Mock.Of<IVideoEditCopyAction>());
        await actions.SaveAsync("/test/video.mp4", CancellationToken.None);

        save.Verify(a => a.ExecuteAsync("/test/video.mp4"), Times.Once);
    }

    [TestMethod]
    public async Task CopyAsync_ShouldDelegateToAction()
    {
        var copy = Fixture.Freeze<Mock<IVideoEditCopyAction>>();

        var actions = new VideoEditActions(Mock.Of<IVideoEditSaveAction>(), copy.Object);
        await actions.CopyAsync("/test/video.mp4", CancellationToken.None);

        copy.Verify(a => a.ExecuteAsync("/test/video.mp4"), Times.Once);
    }
}
