using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.ChangeAudioFolder;
using CaptureTool.Application.Abstractions.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.Settings.UpdateAppTheme;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Settings.UpdateCaptureWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Settings.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.Settings;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class SettingsPageViewModelTelemetryTests
{
    [TestMethod]
    public async Task UpdateTelemetryEnabledCommand_SavesOptInSetting()
    {
        var settings = CreateSettingsService();
        SettingsPageViewModel viewModel = CreateViewModel(settings.Object);

        await viewModel.UpdateTelemetryEnabledCommand.ExecuteAsync(true);

        Assert.IsTrue(viewModel.TelemetryEnabled);
        settings.Verify(
            service => service.Set(CaptureToolSettings.Settings_Telemetry_IsEnabled, true),
            Times.Once);
        settings.Verify(service => service.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ResetTelemetryInstallIdCommand_SavesNewAnonymousIdentifier()
    {
        var settings = CreateSettingsService();
        SettingsPageViewModel viewModel = CreateViewModel(settings.Object);

        await viewModel.ResetTelemetryInstallIdCommand.ExecuteAsync(null);

        settings.Verify(
            service => service.Set(
                CaptureToolSettings.Settings_Telemetry_InstallId,
                It.Is<string>(value => IsGuidN(value))),
            Times.Once);
        settings.Verify(service => service.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static bool IsGuidN(string value)
    {
        return value.Length == 32 && Guid.TryParseExact(value, "N", out _);
    }

    private static Mock<ISettingsService> CreateSettingsService()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.TrySaveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return settings;
    }

    private static SettingsPageViewModel CreateViewModel(ISettingsService settingsService)
    {
        return new SettingsPageViewModel(
            Mock.Of<ILeaveSettingsPageUseCase>(),
            Mock.Of<IRestartSettingsApplicationUseCase>(),
            Mock.Of<IUpdateImageAutoCopyUseCase>(),
            Mock.Of<IUpdateImageAutoSaveUseCase>(),
            Mock.Of<IUpdateAudioCaptureAutoCopyUseCase>(),
            Mock.Of<IUpdateAudioCaptureAutoSaveUseCase>(),
            Mock.Of<IUpdateAudioCaptureDefaultLocalAudioUseCase>(),
            Mock.Of<IUpdateVideoCaptureAutoCopyUseCase>(),
            Mock.Of<IUpdateVideoCaptureAutoSaveUseCase>(),
            Mock.Of<IUpdateVideoCaptureDefaultLocalAudioUseCase>(),
            Mock.Of<IUpdateCaptureWarnBeforeDiscardUseCase>(),
            Mock.Of<IUpdateEditWarnBeforeDiscardUseCase>(),
            Mock.Of<IUpdateAppLanguageUseCase>(),
            Mock.Of<IUpdateAppThemeUseCase>(),
            Mock.Of<IChangeScreenshotsFolderUseCase>(),
            Mock.Of<IOpenScreenshotsFolderUseCase>(),
            Mock.Of<IChangeAudioFolderUseCase>(),
            Mock.Of<IOpenAudioFolderUseCase>(),
            Mock.Of<IChangeVideosFolderUseCase>(),
            Mock.Of<IOpenVideosFolderUseCase>(),
            Mock.Of<IOpenTempFolderUseCase>(),
            Mock.Of<IClearTempFilesUseCase>(),
            Mock.Of<IRestoreDefaultsUseCase>(),
            Mock.Of<ILocalizationService>(),
            Mock.Of<IThemeService>(),
            settingsService,
            Mock.Of<IStorageService>(),
            Mock.Of<IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>>(),
            Mock.Of<IFactoryServiceWithArgs<AppThemeViewModel, AppTheme>>());
    }
}
