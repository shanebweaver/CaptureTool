using CaptureTool.Application.Abstractions.Features.Diagnostics.ClearLogs;
using CaptureTool.Application.Abstractions.Features.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Abstractions.Features.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Abstractions.Features.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Diagnostics.ClearLogs;
using CaptureTool.Application.Features.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Features.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Features.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Features.Settings;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class DiagnosticsUseCaseTests
{
    [TestMethod]
    public async Task ClearLogsUseCase_ClearsLogService()
    {
        var logService = new Mock<ILogService>();
        var useCase = new ClearLogsUseCase(logService.Object, TestUseCaseExecutor.Instance);

        ClearLogsResponse response = (await useCase.ExecuteAsync(new ClearLogsRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        logService.Verify(service => service.ClearLogs(), Times.Once);
    }

    [TestMethod]
    public async Task GetCurrentLogsUseCase_ReturnsLogsFromLogService()
    {
        ILogEntry[] logs = [Mock.Of<ILogEntry>(entry => entry.Message == "entry")];
        var logService = new Mock<ILogService>();
        logService.Setup(service => service.GetLogs()).Returns(logs);
        var useCase = new GetCurrentLogsUseCase(logService.Object, TestUseCaseExecutor.Instance);

        GetCurrentLogsResponse response = (await useCase.ExecuteAsync(new GetCurrentLogsRequest(), TestContext.CancellationToken)).Value!;

        CollectionAssert.AreEqual(logs, response.Logs.ToArray());
    }

    [TestMethod]
    public async Task GetIsLoggingEnabledUseCase_ReturnsLogServiceState()
    {
        var logService = new Mock<ILogService>();
        logService.Setup(service => service.IsEnabled).Returns(true);
        var useCase = new GetIsLoggingEnabledUseCase(logService.Object, TestUseCaseExecutor.Instance);

        GetIsLoggingEnabledResponse response = (await useCase.ExecuteAsync(new GetIsLoggingEnabledRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.IsEnabled);
    }

    [TestMethod]
    public async Task UpdateLoggingStateUseCase_WhenEnabled_EnablesLoggingAndPersistsSetting()
    {
        var logService = new Mock<ILogService>();
        var settings = new Mock<ISettingsService>();
        var useCase = new UpdateLoggingStateUseCase(logService.Object, settings.Object, TestUseCaseExecutor.Instance);

        UpdateLoggingStateResponse response = (await useCase.ExecuteAsync(new UpdateLoggingStateRequest(true), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        logService.Verify(service => service.Enable(), Times.Once);
        logService.Verify(service => service.Disable(), Times.Never);
        settings.Verify(service => service.Set(CaptureToolSettings.VerboseLogging, true), Times.Once);
        settings.Verify(service => service.TrySaveAsync(TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task UpdateLoggingStateUseCase_WhenDisabled_DisablesLoggingAndPersistsSetting()
    {
        var logService = new Mock<ILogService>();
        var settings = new Mock<ISettingsService>();
        var useCase = new UpdateLoggingStateUseCase(logService.Object, settings.Object, TestUseCaseExecutor.Instance);

        UpdateLoggingStateResponse response = (await useCase.ExecuteAsync(new UpdateLoggingStateRequest(false), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        logService.Verify(service => service.Disable(), Times.Once);
        logService.Verify(service => service.Enable(), Times.Never);
        settings.Verify(service => service.Set(CaptureToolSettings.VerboseLogging, false), Times.Once);
        settings.Verify(service => service.TrySaveAsync(TestContext.CancellationToken), Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;
}
