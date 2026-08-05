using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Capture.Video.StartVideoCapture;
using CaptureTool.Domain.Capture;
using Moq;
using System.Drawing;

namespace CaptureTool.Application.Tests.Capture.Video;

[TestClass]
public class StartVideoCaptureUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldStartRecording_WithoutNavigating()
    {
        var navigationService = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigationService);
        var videoCaptureWorkflow = new FakeVideoCaptureWorkflow();
        var useCase = new StartVideoCaptureUseCase(navigationService.Object, videoCaptureWorkflow, TestUseCaseExecutor.Instance);
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

        Assert.AreEqual(args, videoCaptureWorkflow.StartedCaptureArgs);
        navigationService.Verify(
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenWorkflowReportsUnsupported_ReturnsStructuredResult()
    {
        var navigationService = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigationService);
        var videoCaptureWorkflow = new FakeVideoCaptureWorkflow
        {
            StartException = new VideoCaptureNotSupportedException(
                VideoCaptureUnsupportedReason.GraphicsCapture)
        };
        var useCase = new StartVideoCaptureUseCase(
            navigationService.Object,
            videoCaptureWorkflow,
            TestUseCaseExecutor.Instance);
        var args = new NewCaptureArgs(
            new MonitorCaptureResult(
                1,
                [],
                96,
                new Rectangle(0, 0, 1920, 1080),
                new Rectangle(0, 0, 1920, 1040),
                true),
            new Rectangle(10, 20, 300, 200));

        UseCaseResponse<StartVideoCaptureResponse> response = await useCase.ExecuteAsync(
            new StartVideoCaptureRequest(args),
            TestContext.CancellationToken);

        Assert.AreEqual(UseCaseResult.Succeeded, response.Result);
        Assert.IsNotNull(response.Value);
        Assert.IsFalse(response.Value.Succeeded);
        Assert.AreEqual(StartVideoCaptureFailureReason.NotSupported, response.Value.FailureReason);
    }

    public TestContext TestContext { get; set; } = null!;
}
