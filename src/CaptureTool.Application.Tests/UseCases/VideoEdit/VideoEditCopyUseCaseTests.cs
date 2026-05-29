using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Features.VideoEdit;
using CaptureTool.Infrastructure.Abstractions.Clipboard;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.VideoEdit;

[TestClass]
public class VideoEditCopyUseCaseTests
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
        var action = Fixture.Create<CopyVideoFileAppCommand>();

        await action.ExecuteAsync("/test/video.mp4");

        clipboardService.Verify(c => c.CopyFileAsync(It.Is<ClipboardFile>(f => f.FilePath == "/test/video.mp4")), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldThrowWhenVideoPathIsEmpty()
    {
        var action = Fixture.Create<CopyVideoFileAppCommand>();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => action.ExecuteAsync(string.Empty));
    }
}
