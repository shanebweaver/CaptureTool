using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Interfaces.Actions.Settings;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Application.Implementations.ViewModels;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

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

        Fixture.Freeze<Mock<ISettingsGoBackAction>>();
        Fixture.Freeze<Mock<ISettingsRestartAppAction>>();
        Fixture.Freeze<Mock<ISettingsUpdateImageAutoCopyAction>>();
        Fixture.Freeze<Mock<ISettingsUpdateImageAutoSaveAction>>();
        Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoCopyAction>>();
        Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoSaveAction>>();
        Fixture.Freeze<Mock<ISettingsUpdateAppLanguageAction>>();
        Fixture.Freeze<Mock<ISettingsUpdateAppThemeAction>>();
        Fixture.Freeze<Mock<ISettingsChangeScreenshotsFolderAction>>();
        Fixture.Freeze<Mock<ISettingsOpenScreenshotsFolderAction>>();
        Fixture.Freeze<Mock<ISettingsChangeVideosFolderAction>>();
        Fixture.Freeze<Mock<ISettingsOpenVideosFolderAction>>();
        Fixture.Freeze<Mock<ISettingsOpenTempFolderAction>>();
        Fixture.Freeze<Mock<ISettingsClearTempFilesAction>>();
        Fixture.Freeze<Mock<ISettingsRestoreDefaultsAction>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void GoBackCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var goBackAction = Fixture.Freeze<Mock<ISettingsGoBackAction>>();
        var vm = Create();

        vm.GoBackCommand.Execute(null);

        goBackAction.Verify(a => a.Execute(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(SettingsPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(SettingsPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void RestartAppCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var restartAppAction = Fixture.Freeze<Mock<ISettingsRestartAppAction>>();
        var vm = Create();

        vm.RestartAppCommand.Execute(null);

        restartAppAction.Verify(a => a.Execute(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(SettingsPageViewModel.ActivityIds.RestartApp, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(SettingsPageViewModel.ActivityIds.RestartApp, It.IsAny<string>()), Times.Once);
    }
}
