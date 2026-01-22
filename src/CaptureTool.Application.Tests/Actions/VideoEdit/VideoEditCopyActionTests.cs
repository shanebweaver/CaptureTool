using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.Actions.VideoEdit;
using CaptureTool.Infrastructure.Interfaces.Clipboard;
using Moq;

namespace CaptureTool.Application.Tests.Actions.VideoEdit;

[TestClass]
public class VideoEditCopyActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldCopyFileToClipboard()
    {
        var clipboardService = Fixture.Freeze<Mock<IClipboardService>>();
        var action = Fixture.Create<VideoEditCopyAction>();
        
        await action.ExecuteAsync("/test/video.mp4");
        
        clipboardService.Verify(c => c.CopyFileAsync(It.Is<ClipboardFile>(f => f.FilePath == "/test/video.mp4")), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldThrowWhenVideoPathIsEmpty()
    {
        var action = Fixture.Create<VideoEditCopyAction>();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => action.ExecuteAsync(string.Empty));
    }
}
