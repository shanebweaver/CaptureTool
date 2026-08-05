using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Infrastructure.Windows.Shutdown;
using FluentAssertions;
using Moq;
using Windows.ApplicationModel.Core;

namespace CaptureTool.Infrastructure.Windows.Tests.Shutdown;

[TestClass]
public sealed class WindowsShutdownHandlerTests
{
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
