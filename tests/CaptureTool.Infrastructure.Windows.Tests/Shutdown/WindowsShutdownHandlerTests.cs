using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Windows.Shutdown;
using FluentAssertions;
using Moq;
using Windows.ApplicationModel.Core;

namespace CaptureTool.Infrastructure.Windows.Tests.Shutdown;

[TestClass]
public sealed class WindowsShutdownHandlerTests
{
    [TestMethod]
    public void Shutdown_StopsWorkBeforeRequestingApplicationExit_AndRunsOnce()
    {
        var calls = new List<string>();
        var cancellationService = new Mock<ICancellationService>();
        cancellationService.Setup(service => service.CancelAll()).Callback(() => calls.Add("cancel"));
        var captureMemory = new Mock<ICaptureMemoryService>();
        captureMemory.Setup(service => service.StopAsync()).Returns(() =>
        {
            calls.Add("stop");
            return Task.CompletedTask;
        });
        var handler = new WindowsShutdownHandler(
            Mock.Of<ILogService>(), cancellationService.Object,
            new TestWindowsAppRestartService(AppRestartFailureReason.Other),
            captureMemory: captureMemory.Object, exitApplication: () => calls.Add("exit"));

        handler.Shutdown();
        handler.Shutdown();

        handler.IsShuttingDown.Should().BeTrue();
        calls.Should().Equal("stop", "cancel", "exit");
        handler.TryRestart().Should().BeFalse();
    }

    [TestMethod]
    public void Shutdown_WhenTelemetryReenters_DoesNotRepeatTeardownOrExit()
    {
        var telemetry = new Mock<ITelemetryService>();
        var cancellationService = new Mock<ICancellationService>();
        int exitCount = 0;
        var handler = new WindowsShutdownHandler(
            Mock.Of<ILogService>(), cancellationService.Object,
            new TestWindowsAppRestartService(AppRestartFailureReason.Other),
            telemetryService: telemetry.Object, exitApplication: () => exitCount++);
        telemetry.Setup(service => service.TrackEvent(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Callback(() => handler.Shutdown());

        handler.Shutdown();

        cancellationService.Verify(service => service.CancelAll(), Times.Once);
        exitCount.Should().Be(1);
    }

    [TestMethod]
    public void Shutdown_WhenTeardownFails_LogsFailureAndStillRequestsApplicationExit()
    {
        var failure = new InvalidOperationException("cleanup failed");
        var cancellationService = new Mock<ICancellationService>();
        cancellationService.Setup(service => service.CancelAll()).Throws(failure);
        var logService = new Mock<ILogService>();
        int exitCount = 0;
        var handler = new WindowsShutdownHandler(
            logService.Object, cancellationService.Object,
            new TestWindowsAppRestartService(AppRestartFailureReason.Other),
            exitApplication: () => exitCount++);

        handler.Shutdown();
        handler.Shutdown();

        handler.IsShuttingDown.Should().BeTrue();
        logService.Verify(service => service.LogException(failure, "Error during shutdown."), Times.Once);
        exitCount.Should().Be(1);
    }

    [TestMethod]
    [DataRow(AppRestartFailureReason.RestartPending)]
    [DataRow(AppRestartFailureReason.NotInForeground)]
    [DataRow(AppRestartFailureReason.InvalidUser)]
    [DataRow(AppRestartFailureReason.Other)]
    public void TryRestart_WhenWindowsRejectsRequest_PreservesRunningProcess(
        AppRestartFailureReason failureReason)
    {
        var logService = new Mock<ILogService>();
        var cancellationService = new Mock<ICancellationService>();
        var restartService = new TestWindowsAppRestartService(failureReason);
        var handler = new WindowsShutdownHandler(
            logService.Object,
            cancellationService.Object,
            restartService);

        bool restarted = handler.TryRestart();

        restarted.Should().BeFalse();
        handler.IsShuttingDown.Should().BeFalse();
        cancellationService.Verify(service => service.CancelAll(), Times.Never);
        restartService.CallCount.Should().Be(1);
        restartService.Arguments.Should().BeEmpty();
        logService.Verify(service => service.LogWarning(It.IsAny<string>()), Times.Once);
    }

    private sealed class TestWindowsAppRestartService(
        AppRestartFailureReason failureReason) : IWindowsAppRestartService
    {
        public int CallCount { get; private set; }
        public string? Arguments { get; private set; }

        public AppRestartFailureReason Restart(string arguments)
        {
            CallCount++;
            Arguments = arguments;
            return failureReason;
        }
    }
}
