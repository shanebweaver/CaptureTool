using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.VideoEdit;
using CaptureTool.Services.Interfaces.Clipboard;
using Moq;

namespace CaptureTool.Core.Tests.Actions.VideoEdit;

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
