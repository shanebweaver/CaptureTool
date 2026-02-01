using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.ViewModels;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
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

        Fixture.Freeze<Mock<ISettingsGoBackUseCase>>();
        Fixture.Freeze<Mock<ISettingsRestartAppUseCase>>();
        Fixture.Freeze<Mock<ISettingsUpdateImageAutoCopyUseCase>>();
        Fixture.Freeze<Mock<ISettingsUpdateImageAutoSaveUseCase>>();
        Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoCopyUseCase>>();
        Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoSaveUseCase>>();
        Fixture.Freeze<Mock<ISettingsUpdateVideoMetadataAutoSaveUseCase>>();
        Fixture.Freeze<Mock<ISettingsUpdateAppLanguageUseCase>>();
        Fixture.Freeze<Mock<ISettingsUpdateAppThemeUseCase>>();
        Fixture.Freeze<Mock<ISettingsChangeScreenshotsFolderUseCase>>();
        Fixture.Freeze<Mock<ISettingsOpenScreenshotsFolderUseCase>>();
        Fixture.Freeze<Mock<ISettingsChangeVideosFolderUseCase>>();
        Fixture.Freeze<Mock<ISettingsOpenVideosFolderUseCase>>();
        Fixture.Freeze<Mock<ISettingsOpenTempFolderUseCase>>();
        Fixture.Freeze<Mock<ISettingsClearTempFilesUseCase>>();
        Fixture.Freeze<Mock<ISettingsRestoreDefaultsUseCase>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void GoBackCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var goBackAction = Fixture.Freeze<Mock<ISettingsGoBackUseCase>>();
        var vm = Create();

        vm.GoBackCommand.Execute();

        goBackAction.Verify(a => a.Execute(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(SettingsPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(SettingsPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void RestartAppCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var restartAppAction = Fixture.Freeze<Mock<ISettingsRestartAppUseCase>>();
        var vm = Create();

        vm.RestartAppCommand.Execute();

        restartAppAction.Verify(a => a.Execute(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(SettingsPageViewModel.ActivityIds.RestartApp, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(SettingsPageViewModel.ActivityIds.RestartApp, It.IsAny<string>()), Times.Once);
    }
}
