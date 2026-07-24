using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using Moq;

namespace CaptureTool.Application.Tests.UseCases;

[TestClass]
public sealed class UseCaseExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnSuccessAndTrackTelemetry()
    {
        var logService = new Mock<ILogService>();
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(logService.Object, telemetry.Object);

        UseCaseResponse<string> response = await executor.ExecuteAsync(
            "activity",
            _ => Task.FromResult("done"),
            TestContext.CancellationToken);

        Assert.AreEqual(UseCaseResult.Succeeded, response.Result);
        Assert.AreEqual("done", response.Value);
        VerifyAction(telemetry, TelemetryOutcomes.Succeeded);
        logService.Verify(service => service.LogInformation("Activity initiated: activity"), Times.Once);
        logService.Verify(service => service.LogInformation("Activity completed: activity"), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnCancelledWithoutExecuting_WhenTokenIsAlreadyCanceled()
    {
        var logService = new Mock<ILogService>();
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(logService.Object, telemetry.Object);
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
        VerifyAction(telemetry, TelemetryOutcomes.Canceled);
        logService.Verify(service => service.LogInformation("Activity canceled: activity"), Times.Once);
        logService.Verify(service => service.LogInformation("Activity completed: activity"), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnCancelled_WhenUseCaseThrowsOperationCanceledException()
    {
        var logService = new Mock<ILogService>();
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(logService.Object, telemetry.Object);

        UseCaseResponse<string> response = await executor.ExecuteAsync<string>(
            "activity",
            _ => throw new OperationCanceledException("stopped"),
            TestContext.CancellationToken);

        Assert.AreEqual(UseCaseResult.Cancelled, response.Result);
        VerifyAction(telemetry, TelemetryOutcomes.Canceled);
        logService.Verify(
            service => service.LogInformation("Activity canceled: activity - Message: stopped"),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenUseCaseThrowsUnexpectedException()
    {
        var logService = new Mock<ILogService>();
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(logService.Object, telemetry.Object);
        var exception = new InvalidOperationException("failed");

        UseCaseResponse<string> response = await executor.ExecuteAsync<string>(
            "activity",
            _ => throw exception,
            TestContext.CancellationToken);

        Assert.AreEqual(UseCaseResult.Failed, response.Result);
        Assert.IsNull(response.Value);
        VerifyAction(telemetry, TelemetryOutcomes.Failed);
        logService.Verify(
            service => service.LogException(exception, "Activity error: activity"),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldRunSynchronousUseCaseThroughTelemetryPipeline()
    {
        var logService = new Mock<ILogService>();
        var telemetry = new Mock<ITelemetryService>();
        IUseCaseExecutor executor = new UseCaseExecutor(logService.Object, telemetry.Object);

        UseCaseResponse<int> response = await executor.ExecuteAsync(
            "activity",
            () => 42,
            TestContext.CancellationToken);

        Assert.AreEqual(UseCaseResult.Succeeded, response.Result);
        Assert.AreEqual(42, response.Value);
        VerifyAction(telemetry, TelemetryOutcomes.Succeeded);
    }

    private static void VerifyAction(
        Mock<ITelemetryService> telemetry,
        string outcome)
    {
        telemetry.Verify(
            service => service.TrackEvent(
                TelemetryEvents.UseCaseCompleted,
                It.Is<IReadOnlyDictionary<string, object?>?>(
                    properties => IsAction(properties, "activity", outcome))),
            Times.Once);
        telemetry.Verify(
            service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()),
            Times.Once);
    }

    private static bool IsAction(
        IReadOnlyDictionary<string, object?>? properties,
        string action,
        string outcome)
    {
        return properties is not null
            && properties.TryGetValue(TelemetryProperties.Action, out object? actualAction)
            && properties.TryGetValue(TelemetryProperties.Outcome, out object? actualOutcome)
            && Equals(actualAction, action)
            && Equals(actualOutcome, outcome);
    }

    public TestContext TestContext { get; set; } = null!;
}
