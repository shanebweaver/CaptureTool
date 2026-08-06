using CaptureTool.Application.Abstractions.Diagnostics.ClearLogs;
using CaptureTool.Application.Abstractions.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Abstractions.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Abstractions.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Presentation.Features.Diagnostics;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class DiagnosticsViewModelTests
{
    [TestMethod]
    public void LogAdded_ShouldMarshalRenderedTextThroughTaskEnvironment()
    {
        var logService = new Mock<ILogService>();
        var taskEnvironment = new Mock<ITaskEnvironment>();
        Action? dispatchedAction = null;
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => dispatchedAction = action)
            .Returns(true);
        DiagnosticsViewModel viewModel = CreateViewModel(logService, taskEnvironment);

        logService.Raise(
            service => service.LogAdded += null,
            logService.Object,
            new TestLogEntry("background log"));

        viewModel.Logs.Should().BeEmpty();
        dispatchedAction.Should().NotBeNull();

        dispatchedAction!();

        viewModel.Logs.Should().Be("background log");
        viewModel.Dispose();
    }

    [TestMethod]
    public void Dispose_ShouldUnsubscribeAndIgnoreQueuedLogUpdates()
    {
        var logService = new Mock<ILogService>();
        var taskEnvironment = new Mock<ITaskEnvironment>();
        Action? dispatchedAction = null;
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => dispatchedAction = action)
            .Returns(true);
        DiagnosticsViewModel viewModel = CreateViewModel(logService, taskEnvironment);
        logService.Raise(
            service => service.LogAdded += null,
            logService.Object,
            new TestLogEntry("queued log"));

        viewModel.Dispose();
        viewModel.Dispose();
        dispatchedAction!();
        logService.Raise(
            service => service.LogAdded += null,
            logService.Object,
            new TestLogEntry("late log"));

        viewModel.Logs.Should().BeEmpty();
        taskEnvironment.Verify(
            environment => environment.TryExecute(It.IsAny<Action>()),
            Times.Once);
        logService.VerifyRemove(
            service => service.LogAdded -= It.IsAny<EventHandler<ILogEntry>>(),
            Times.Once);
    }

    [TestMethod]
    public void LogAdded_WhenRenderedHistoryIsFull_ShouldRetainNewestEntries()
    {
        var logService = new Mock<ILogService>();
        var taskEnvironment = new Mock<ITaskEnvironment>();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);
        DiagnosticsViewModel viewModel = CreateViewModel(logService, taskEnvironment);

        for (int index = 0; index < 1005; index++)
        {
            logService.Raise(
                service => service.LogAdded += null,
                logService.Object,
                new TestLogEntry($"log-{index}"));
        }

        string[] renderedLogs = viewModel.Logs.Split(Environment.NewLine);
        renderedLogs.Should().HaveCount(1000);
        renderedLogs[0].Should().Be("log-5");
        renderedLogs[^1].Should().Be("log-1004");
        viewModel.Dispose();
    }

    private static DiagnosticsViewModel CreateViewModel(
        Mock<ILogService> logService,
        Mock<ITaskEnvironment> taskEnvironment)
    {
        var getIsLoggingEnabledUseCase = new Mock<IGetIsLoggingEnabledUseCase>();
        getIsLoggingEnabledUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<GetIsLoggingEnabledRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<GetIsLoggingEnabledResponse>.Success(new(false)));
        var getCurrentLogsUseCase = new Mock<IGetCurrentLogsUseCase>();
        getCurrentLogsUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<GetCurrentLogsRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<GetCurrentLogsResponse>.Success(new([])));

        return new DiagnosticsViewModel(
            Mock.Of<IClearLogsUseCase>(),
            Mock.Of<IExportLogsUseCase>(),
            Mock.Of<IUpdateLoggingStateUseCase>(),
            getIsLoggingEnabledUseCase.Object,
            getCurrentLogsUseCase.Object,
            logService.Object,
            taskEnvironment.Object);
    }

    private sealed class TestLogEntry(string message) : ILogEntry
    {
        public string Message => message;
        public DateTime Timestamp => DateTime.UtcNow;
        public override string ToString() => Message;
    }
}
