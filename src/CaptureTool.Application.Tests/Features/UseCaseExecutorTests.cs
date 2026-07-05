using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class UseCaseExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnSuccessAndTrackTelemetry()
    {
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(telemetry.Object);

        UseCaseResponse<string> response = await executor.ExecuteAsync(
            "activity",
            _ => Task.FromResult("done"),
            TestContext.CancellationToken);

        Assert.AreEqual(UseCaseResult.Succeeded, response.Result);
        Assert.AreEqual("done", response.Value);
        telemetry.Verify(service => service.ActivityInitiated("activity", null), Times.Once);
        telemetry.Verify(service => service.ActivityCompleted("activity", null), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnCancelledWithoutExecuting_WhenTokenIsAlreadyCanceled()
    {
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(telemetry.Object);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        bool executed = false;

        UseCaseResponse<string> response = await executor.ExecuteAsync(
            "activity",
            _ =>
            {
                executed = true;
                return Task.FromResult("done");
            },
            cancellationTokenSource.Token);

        Assert.AreEqual(UseCaseResult.Cancelled, response.Result);
        Assert.IsFalse(executed);
        telemetry.Verify(service => service.ActivityInitiated("activity", null), Times.Once);
        telemetry.Verify(service => service.ActivityCanceled("activity", null), Times.Once);
        telemetry.Verify(service => service.ActivityCompleted(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnCancelled_WhenUseCaseThrowsOperationCanceledException()
    {
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(telemetry.Object);

        UseCaseResponse<string> response = await executor.ExecuteAsync<string>(
            "activity",
            _ => throw new OperationCanceledException("stopped"),
            TestContext.CancellationToken);

        Assert.AreEqual(UseCaseResult.Cancelled, response.Result);
        telemetry.Verify(service => service.ActivityCanceled("activity", "stopped"), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenUseCaseThrowsUnexpectedException()
    {
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(telemetry.Object);
        var exception = new InvalidOperationException("failed");

        UseCaseResponse<string> response = await executor.ExecuteAsync<string>(
            "activity",
            _ => throw exception,
            TestContext.CancellationToken);

        Assert.AreEqual(UseCaseResult.Failed, response.Result);
        Assert.IsNull(response.Value);
        telemetry.Verify(
            service => service.ActivityError("activity", exception, null, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldRunSynchronousUseCaseThroughTelemetryPipeline()
    {
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(telemetry.Object);

        UseCaseResponse<int> response = await executor.ExecuteAsync(
            "activity",
            () => 42,
            TestContext.CancellationToken);

        Assert.AreEqual(UseCaseResult.Succeeded, response.Result);
        Assert.AreEqual(42, response.Value);
        telemetry.Verify(service => service.ActivityInitiated("activity", null), Times.Once);
        telemetry.Verify(service => service.ActivityCompleted("activity", null), Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;
}
