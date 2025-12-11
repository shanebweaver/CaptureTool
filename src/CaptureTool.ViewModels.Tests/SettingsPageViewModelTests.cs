using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels;
using Moq;

namespace CaptureTool.ViewModels.Tests;

[TestClass]
public sealed class SettingsPageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private SettingsPageViewModel Create() => Fixture.Create<SettingsPageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Freeze<Mock<ISettingsActions>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void GoBackCommand_ShouldDelegateToActions_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var actions = Fixture.Freeze<Mock<ISettingsActions>>();
        var vm = Create();

        vm.GoBackCommand.Execute(null);

        actions.Verify(a => a.GoBack(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(SettingsPageViewModel.ActivityIds.GoBack), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(SettingsPageViewModel.ActivityIds.GoBack), Times.Once);
    }

    [TestMethod]
    public void RestartAppCommand_ShouldDelegateToActions_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var actions = Fixture.Freeze<Mock<ISettingsActions>>();
        var vm = Create();

        vm.RestartAppCommand.Execute(null);

        actions.Verify(a => a.RestartApp(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(SettingsPageViewModel.ActivityIds.RestartApp), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(SettingsPageViewModel.ActivityIds.RestartApp), Times.Once);
    }
}
