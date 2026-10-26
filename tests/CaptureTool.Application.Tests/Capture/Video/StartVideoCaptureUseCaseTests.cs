using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
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
    public async Task ExecuteAsync_WhenNativeStartupWaits_LeavesCallingThreadAvailable()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var returned = new ManualResetEventSlim();
        var workflow = new FakeVideoCaptureWorkflow
        {
            OnStart = () => { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); }
        };
        var useCase = new StartVideoCaptureUseCase(Mock.Of<INavigationService>(), workflow, TestUseCaseExecutor.Instance);
        Task<UseCaseResponse<StartVideoCaptureResponse>>? pending = null;
        var caller = new Thread(() =>
        {
            pending = useCase.ExecuteAsync(new StartVideoCaptureRequest(default));
            returned.Set();
        }) { IsBackground = true };
        caller.Start();
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)), "Startup should begin.");
            Assert.IsTrue(returned.Wait(TimeSpan.FromSeconds(2)), "The UI caller must return while native startup is pending.");
            Assert.IsNotNull(pending);
            Assert.IsFalse(pending.IsCompleted);
        }
        finally
        {
            release.Set();
            caller.Join(TimeSpan.FromSeconds(5));
        }
        Assert.IsNotNull(pending);
        Assert.AreEqual(UseCaseResult.Succeeded, (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Result);
    }

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
