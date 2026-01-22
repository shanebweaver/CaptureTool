using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.Actions.CaptureOverlay;
using CaptureTool.Domain.Capture.Interfaces;
using Moq;

namespace CaptureTool.Application.Tests.Actions.CaptureOverlay;

[TestClass]
public class CaptureOverlayToggleDesktopAudioActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldToggleDesktopAudio()
    {
        var handler = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        handler.SetupGet(h => h.IsDesktopAudioEnabled).Returns(false);

        var action = Fixture.Create<CaptureOverlayToggleDesktopAudioAction>();
        action.Execute();

        handler.Verify(h => h.SetIsDesktopAudioEnabled(true), Times.Once);
        handler.Verify(h => h.ToggleDesktopAudioCapture(true), Times.Once);
    }
}
