using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Metrics;
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
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Ai;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.Settings;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class SettingsPageViewModelAiConsentTests
{
    [TestMethod]
    public async Task LoadAsync_WithTelemetryConsent_ShouldEnableOptionalUsageData()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            telemetryConsentValue: TelemetryConsentSettingValues.Granted);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.OptionalUsageDataEnabled.Should().BeTrue();
    }

    [TestMethod]
    public async Task UpdateOptionalUsageDataEnabledCommand_ShouldUpdateConsentGate()
    {
        var telemetryConsent = new Mock<ITelemetryConsentService>();
        SettingsPageViewModel viewModel = CreateViewModel(
            telemetryConsentService: telemetryConsent.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.UpdateOptionalUsageDataEnabledCommand.ExecuteAsync(true);

        viewModel.OptionalUsageDataEnabled.Should().BeTrue();
        telemetryConsent.Verify(
            service => service.SetState(TelemetryConsentState.Granted),
            Times.Once);
    }

    [TestMethod]
    public async Task UpdateOptionalUsageDataEnabledCommand_WhenPersistenceFails_RevertsConsentGate()
    {
        var telemetryConsent = new Mock<ITelemetryConsentService>();
        SettingsPageViewModel viewModel = CreateViewModel(
            telemetryConsentService: telemetryConsent.Object,
            settingsMutationStatus: SettingsMutationStatus.PersistenceFailed);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.UpdateOptionalUsageDataEnabledCommand.ExecuteAsync(true);

        viewModel.OptionalUsageDataEnabled.Should().BeFalse();
        telemetryConsent.Verify(
            service => service.SetState(It.IsAny<TelemetryConsentState>()),
            Times.Never);
    }

    [TestMethod]
    public async Task UpdateImageCaptureAutoSaveCommand_WhenPersistenceFails_RevertsOptimisticValue()
    {
        var updateAction = new Mock<IUpdateImageAutoSaveUseCase>();
        updateAction
            .Setup(action => action.ExecuteAsync(
                new UpdateImageAutoSaveRequest(false),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<UpdateImageAutoSaveResponse>.Success(
                new UpdateImageAutoSaveResponse(false)));
        SettingsPageViewModel viewModel = CreateViewModel(
            updateImageAutoSaveAction: updateAction.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.UpdateImageCaptureAutoSaveCommand.ExecuteAsync(false);

        viewModel.ImageCaptureAutoSave.Should().BeTrue();
    }

    [TestMethod]
    public async Task RestoreDefaultSettingsCommand_WhenRestoreFails_ShouldKeepDisplayedTheme()
    {
        var restoreDefaults = new Mock<IRestoreDefaultsUseCase>();
        restoreDefaults
            .Setup(service => service.ExecuteAsync(It.IsAny<RestoreDefaultsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<RestoreDefaultsResponse>.Failure());
        SettingsPageViewModel viewModel = CreateViewModel(restoreDefaultsUseCase: restoreDefaults.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);
        int selectedThemeIndex = viewModel.SelectedAppThemeIndex;

        await viewModel.RestoreDefaultSettingsCommand.ExecuteAsync(null);

        viewModel.SelectedAppThemeIndex.Should().Be(selectedThemeIndex);
    }

    private static SettingsPageViewModel CreateViewModel(
        string telemetryConsentValue = TelemetryConsentSettingValues.Unknown,
        ITelemetryConsentService? telemetryConsentService = null,
        SettingsMutationStatus settingsMutationStatus = SettingsMutationStatus.Saved,
        IUpdateImageAutoSaveUseCase? updateImageAutoSaveAction = null,
        IRestoreDefaultsUseCase? restoreDefaultsUseCase = null)
    {
        var localization = new Mock<ILocalizationService>();
        localization
            .Setup(service => service.SupportedLanguages)
            .Returns([]);
        localization
            .Setup(service => service.GetString(It.IsAny<string>()))
            .Returns<string>(resourceKey => resourceKey);

        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(It.IsAny<ISettingDefinitionWithValue<bool>>()))
            .Returns<ISettingDefinitionWithValue<bool>>(definition => definition.Value);
        settings
            .Setup(service => service.Get(It.IsAny<ISettingDefinitionWithValue<string>>()))
            .Returns<ISettingDefinitionWithValue<string>>(definition => definition.Value);
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_TelemetryConsent))
            .Returns(telemetryConsentValue);
        settings
            .Setup(service => service.TrySetAndSaveAsync(
                It.IsAny<IStringSettingDefinition>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsMutationResult(settingsMutationStatus));

        var appLanguageViewModelFactory = new Mock<IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>>();
        appLanguageViewModelFactory
            .Setup(factory => factory.Create(It.IsAny<IAppLanguage?>()))
            .Returns<IAppLanguage?>(language => new AppLanguageViewModel(language, localization.Object));

        var appThemeViewModelFactory = new Mock<IFactoryServiceWithArgs<AppThemeViewModel, AppTheme>>();
        appThemeViewModelFactory
            .Setup(factory => factory.Create(It.IsAny<AppTheme>()))
            .Returns<AppTheme>(theme => new AppThemeViewModel(theme, localization.Object));

        var storage = new Mock<IStorageService>();
        storage
            .Setup(service => service.GetSystemDefaultScreenshotsFolderPath())
            .Returns(@"C:\Screenshots");
        storage
            .Setup(service => service.GetSystemDefaultVideosFolderPath())
            .Returns(@"C:\Videos");
        storage
            .Setup(service => service.GetSystemDefaultMusicFolderPath())
            .Returns(@"C:\Music");
        storage
            .Setup(service => service.GetApplicationScratchFolderPath())
            .Returns(@"C:\Temp");

        return new SettingsPageViewModel(
            Mock.Of<ILeaveSettingsPageUseCase>(),
            Mock.Of<IRestartSettingsApplicationUseCase>(),
            Mock.Of<IUpdateImageAutoCopyUseCase>(),
            updateImageAutoSaveAction ?? Mock.Of<IUpdateImageAutoSaveUseCase>(),
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
            restoreDefaultsUseCase ?? Mock.Of<IRestoreDefaultsUseCase>(),
            localization.Object,
            Mock.Of<IThemeService>(service =>
                service.DefaultTheme == AppTheme.Light &&
                service.StartupTheme == AppTheme.Light &&
                service.CurrentTheme == AppTheme.Light),
            settings.Object,
            Mock.Of<IAppMetricsService>(),
            Mock.Of<IStoreService>(),
            storage.Object,
            appLanguageViewModelFactory.Object,
            appThemeViewModelFactory.Object,
            telemetryConsentService);
    }

    public TestContext TestContext { get; set; } = null!;
}
