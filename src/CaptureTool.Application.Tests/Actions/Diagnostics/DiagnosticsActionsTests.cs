using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.Actions.Diagnostics;
using CaptureTool.Application.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Settings;
using FluentAssertions;
using Moq;

namespace CaptureTool.Application.Tests.Actions.Diagnostics;

[TestClass]
public class DiagnosticsActionsTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public async Task UpdateLoggingStateAsync_WithEnabled_EnablesLogging()
    {
        var logService = Fixture.Freeze<Mock<ILogService>>();
        var settingsService = Fixture.Freeze<Mock<ISettingsService>>();

        var actions = Fixture.Create<DiagnosticsActions>();
        await actions.UpdateLoggingStateAsync(true, CancellationToken.None);

        logService.Verify(s => s.Enable(), Times.Once);
        logService.Verify(s => s.Disable(), Times.Never);
        settingsService.Verify(s => s.Set(CaptureToolSettings.VerboseLogging, true), Times.Once);
        settingsService.Verify(s => s.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UpdateLoggingStateAsync_WithDisabled_DisablesLogging()
    {
        var logService = Fixture.Freeze<Mock<ILogService>>();
        var settingsService = Fixture.Freeze<Mock<ISettingsService>>();

        var actions = Fixture.Create<DiagnosticsActions>();
        await actions.UpdateLoggingStateAsync(false, CancellationToken.None);

        logService.Verify(s => s.Disable(), Times.Once);
        logService.Verify(s => s.Enable(), Times.Never);
        settingsService.Verify(s => s.Set(CaptureToolSettings.VerboseLogging, false), Times.Once);
        settingsService.Verify(s => s.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void ClearLogs_CallsLogServiceClearLogs()
    {
        var logService = Fixture.Freeze<Mock<ILogService>>();

        var actions = Fixture.Create<DiagnosticsActions>();
        actions.ClearLogs();

        logService.Verify(s => s.ClearLogs(), Times.Once);
    }

    [TestMethod]
    public void GetCurrentLogs_ReturnsLogsFromService()
    {
        var logService = Fixture.Freeze<Mock<ILogService>>();
        var expectedLogs = Fixture.CreateMany<ILogEntry>().ToList();
        logService.Setup(s => s.GetLogs()).Returns(expectedLogs);

        var actions = Fixture.Create<DiagnosticsActions>();
        var result = actions.GetCurrentLogs();

        result.Should().BeEquivalentTo(expectedLogs);
        logService.Verify(s => s.GetLogs(), Times.Once);
    }

    [TestMethod]
    public void IsLoggingEnabled_ReturnsFromLogService()
    {
        var logService = Fixture.Freeze<Mock<ILogService>>();
        logService.Setup(s => s.IsEnabled).Returns(true);

        var actions = Fixture.Create<DiagnosticsActions>();
        var result = actions.IsLoggingEnabled();

        result.Should().BeTrue();
    }
}
