using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Domain.Capture;
using Moq;
using System.Drawing;

namespace CaptureTool.Application.Tests.Features.CaptureOverlay;

[TestClass]
public class StartVideoCaptureUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldStartRecording_WithoutNavigating()
    {
        var navigationService = new Mock<INavigationService>();
        var videoCaptureHandler = new Mock<IVideoCaptureHandler>();
        var useCase = new StartVideoCaptureUseCase(navigationService.Object, videoCaptureHandler.Object);
        var args = new NewCaptureArgs(
            new MonitorCaptureResult(
                1,
                [],
                96,
                new Rectangle(0, 0, 1920, 1080),
                new Rectangle(0, 0, 1920, 1040),
                true),
            new Rectangle(10, 20, 300, 200));

        await useCase.ExecuteAsync(new StartVideoCaptureRequest(args), TestContext.CancellationToken);

        videoCaptureHandler.Verify(handler => handler.StartVideoCapture(args), Times.Once);
        navigationService.Verify(
            service => service.Navigate(It.IsAny<object>(), It.IsAny<object?>(), It.IsAny<bool>()),
            Times.Never);
    }

    public TestContext TestContext { get; set; }
}
