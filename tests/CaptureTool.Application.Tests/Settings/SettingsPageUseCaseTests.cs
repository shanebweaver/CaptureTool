using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
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
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Settings.ChangeAudioFolder;
using CaptureTool.Application.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Settings.ChangeVideosFolder;
using CaptureTool.Application.Settings.ClearTempFiles;
using CaptureTool.Application.Settings.LeaveSettingsPage;
using CaptureTool.Application.Settings.OpenAudioFolder;
using CaptureTool.Application.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Settings.OpenTempFolder;
using CaptureTool.Application.Settings.OpenVideosFolder;
using CaptureTool.Application.Settings.RestartSettingsApplication;
using CaptureTool.Application.Settings.RestoreDefaults;
using CaptureTool.Application.Settings.UpdateAppLanguage;
using CaptureTool.Application.Settings.UpdateAudioCaptureAutoCopy;
using CaptureTool.Application.Settings.UpdateAudioCaptureAutoSave;
using CaptureTool.Application.Settings.UpdateAudioCaptureDefaultLocalAudio;
using CaptureTool.Application.Settings.UpdateCaptureWarnBeforeDiscard;
using CaptureTool.Application.Settings.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Settings.UpdateVideoCaptureDefaultLocalAudio;
using Moq;

namespace CaptureTool.Application.Tests.Settings;

[TestClass]
public sealed class SettingsPageUseCaseTests
{
    [TestMethod]
    public async Task ChangeScreenshotsFolderUseCase_WhenFolderSelected_SavesFolderSetting()
    {
        var picker = new Mock<IFilePickerService>();
        Mock<ISettingsService> settings = CreatePersistingSettings();
        picker
            .Setup(service => service.PickFolderAsync(UserFolder.Pictures))
            .ReturnsAsync(Mock.Of<IFolder>(folder => folder.FolderPath == @"C:\Screenshots"));
        var useCase = new ChangeScreenshotsFolderUseCase(picker.Object, settings.Object, TestUseCaseExecutor.Instance);

        ChangeScreenshotsFolderResponse response = (await useCase.ExecuteAsync(new ChangeScreenshotsFolderRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Changed);
        settings.Verify(service => service.TrySetAndSaveAsync(
            CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder,
            @"C:\Screenshots",
            TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task ChangeVideosFolderUseCase_WhenPickerCanceled_DoesNotSaveSetting()
    {
        var picker = new Mock<IFilePickerService>();
        Mock<ISettingsService> settings = CreatePersistingSettings();
        picker
            .Setup(service => service.PickFolderAsync(UserFolder.Videos))
            .ReturnsAsync((IFolder?)null);
        var useCase = new ChangeVideosFolderUseCase(picker.Object, settings.Object, TestUseCaseExecutor.Instance);

        ChangeVideosFolderResponse response = (await useCase.ExecuteAsync(new ChangeVideosFolderRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Changed);
        settings.Verify(service => service.TrySetAndSaveAsync(
            It.IsAny<IStringSettingDefinition>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ChangeAudioFolderUseCase_WhenFolderSelected_SavesFolderSetting()
    {
        var picker = new Mock<IFilePickerService>();
        Mock<ISettingsService> settings = CreatePersistingSettings();
        picker
            .Setup(service => service.PickFolderAsync(UserFolder.Music))
            .ReturnsAsync(Mock.Of<IFolder>(folder => folder.FolderPath == @"C:\Audio"));
        var useCase = new ChangeAudioFolderUseCase(picker.Object, settings.Object, TestUseCaseExecutor.Instance);

        ChangeAudioFolderResponse response = (await useCase.ExecuteAsync(new ChangeAudioFolderRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Changed);
        settings.Verify(service => service.TrySetAndSaveAsync(
            CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder,
            @"C:\Audio",
            TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task ClearTempFilesUseCase_DeletesFilesAndFoldersInTemporaryFolder()
    {
        string tempFolder = CreateTestFolder();
        string filePath = Path.Combine(tempFolder, "capture.tmp");
        string directoryPath = Path.Combine(tempFolder, "nested");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllTextAsync(filePath, "file", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directoryPath, "child.tmp"), "child", TestContext.CancellationToken);
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(tempFolder);
        var recentCapturesChangeNotifier = new Mock<IRecentCapturesChangeNotifier>();
        var useCase = new ClearTempFilesUseCase(
            Mock.Of<ILogService>(),
            storage.Object,
            TestFileSystem.Instance,
            TestUseCaseExecutor.Instance,
            recentCapturesChangeNotifier.Object);

        ClearTempFilesResponse response = (await useCase.ExecuteAsync(new ClearTempFilesRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(tempFolder).ToArray());
        recentCapturesChangeNotifier.Verify(
            notifier => notifier.NotifyRecentCapturesChanged(),
            Times.Once);
    }

    [TestMethod]
    public async Task LeaveSettingsPageUseCase_WhenCannotGoBack_NavigatesHomeAndClearsHistory()
    {
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation
            .Setup(service => service.TryGoBackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.NoChange);
        INavigationCoordinator coordinator = TestNavigationCoordinator.Create(navigation.Object);
        var useCase = new LeaveSettingsPageUseCase(coordinator, TestUseCaseExecutor.Instance);

        LeaveSettingsPageResponse response = (await useCase.ExecuteAsync(new LeaveSettingsPageRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        navigation.Verify(
            service => service.NavigateAsync(NavigationRoute.Home, null, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task OpenFolderUseCases_WhenFoldersAreMissing_ReturnNotOpened()
    {
        string missingFolder = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Mock<ISettingsService> settings = CreatePersistingSettings();
        var storage = new Mock<IStorageService>();
        var folderLauncher = new Mock<IFolderLauncher>();
        settings.Setup(service => service.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder)).Returns("");
        settings.Setup(service => service.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder)).Returns("");
        settings.Setup(service => service.Get(CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder)).Returns("");
        storage.Setup(service => service.GetSystemDefaultScreenshotsFolderPath()).Returns(missingFolder);
        storage.Setup(service => service.GetSystemDefaultVideosFolderPath()).Returns(missingFolder);
        storage.Setup(service => service.GetSystemDefaultMusicFolderPath()).Returns(missingFolder);
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(missingFolder);
        folderLauncher.Setup(service => service.TryOpenFolder(missingFolder)).Returns(false);

        var screenshots = new OpenScreenshotsFolderUseCase(settings.Object, storage.Object, folderLauncher.Object, TestUseCaseExecutor.Instance);
        var videos = new OpenVideosFolderUseCase(settings.Object, storage.Object, folderLauncher.Object, TestUseCaseExecutor.Instance);
        var audio = new OpenAudioFolderUseCase(settings.Object, storage.Object, folderLauncher.Object, TestUseCaseExecutor.Instance);
        var temp = new OpenTempFolderUseCase(storage.Object, folderLauncher.Object, TestUseCaseExecutor.Instance);

        OpenScreenshotsFolderResponse screenshotsResponse = (await screenshots.ExecuteAsync(new OpenScreenshotsFolderRequest(), TestContext.CancellationToken)).Value!;
        OpenVideosFolderResponse videosResponse = (await videos.ExecuteAsync(new OpenVideosFolderRequest(), TestContext.CancellationToken)).Value!;
        OpenAudioFolderResponse audioResponse = (await audio.ExecuteAsync(new OpenAudioFolderRequest(), TestContext.CancellationToken)).Value!;
        OpenTempFolderResponse tempResponse = (await temp.ExecuteAsync(new OpenTempFolderRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(screenshotsResponse.Opened);
        Assert.IsFalse(videosResponse.Opened);
        Assert.IsFalse(audioResponse.Opened);
        Assert.IsFalse(tempResponse.Opened);
    }

    [TestMethod]
    public async Task RestartSettingsApplicationUseCase_RespectsShutdownStateAndRestarts()
    {
        var shutdown = new Mock<IShutdownHandler>();
        var useCase = new RestartSettingsApplicationUseCase(shutdown.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new RestartSettingsApplicationRequest()));
        RestartSettingsApplicationResponse response = (await useCase.ExecuteAsync(new RestartSettingsApplicationRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        shutdown.Verify(handler => handler.TryRestart(), Times.Once);

        shutdown.Setup(handler => handler.IsShuttingDown).Returns(true);
        Assert.IsFalse(useCase.CanExecute(new RestartSettingsApplicationRequest()));
    }

    [TestMethod]
    public async Task RestoreDefaultsUseCase_ClearsSettingsLanguageOverrideAndSaves()
    {
        Mock<ISettingsService> settings = CreatePersistingSettings();
        var localization = new Mock<ILocalizationService>();
        var telemetryConsent = new Mock<ITelemetryConsentService>();
        var useCase = new RestoreDefaultsUseCase(
            settings.Object,
            localization.Object,
            telemetryConsent.Object,
            TestUseCaseExecutor.Instance);

        RestoreDefaultsResponse response = (await useCase.ExecuteAsync(new RestoreDefaultsRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        settings.Verify(service => service.TryClearAllAndSaveAsync(TestContext.CancellationToken), Times.Once);
        localization.Verify(service => service.OverrideLanguage(null), Times.Once);
        telemetryConsent.Verify(
            service => service.SetState(TelemetryConsentState.Unknown),
            Times.Once);
    }

    [TestMethod]
    public async Task RestoreDefaultsUseCase_WhenPersistenceFails_DoesNotApplyRuntimeDefaults()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.TryClearAllAndSaveAsync(TestContext.CancellationToken))
            .ReturnsAsync(SettingsMutationResult.PersistenceFailed);
        var localization = new Mock<ILocalizationService>();
        var telemetryConsent = new Mock<ITelemetryConsentService>();
        var useCase = new RestoreDefaultsUseCase(
            settings.Object,
            localization.Object,
            telemetryConsent.Object,
            TestUseCaseExecutor.Instance);

        RestoreDefaultsResponse response = (await useCase.ExecuteAsync(
            new RestoreDefaultsRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        localization.Verify(service => service.OverrideLanguage(It.IsAny<IAppLanguage?>()), Times.Never);
        telemetryConsent.Verify(
            service => service.SetState(It.IsAny<TelemetryConsentState>()),
            Times.Never);
    }

    [TestMethod]
    public async Task UpdateAppLanguageUseCase_WithSupportedLanguage_OverridesLanguageAndSavesSetting()
    {
        var language = Mock.Of<IAppLanguage>(appLanguage => appLanguage.Value == "fr-FR");
        var localization = new Mock<ILocalizationService>();
        Mock<ISettingsService> settings = CreatePersistingSettings();
        localization.Setup(service => service.SupportedLanguages).Returns([language]);
        var useCase = new UpdateAppLanguageUseCase(localization.Object, settings.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new UpdateAppLanguageRequest(0)));
        UpdateAppLanguageResponse response = (await useCase.ExecuteAsync(new UpdateAppLanguageRequest(0), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        localization.Verify(service => service.OverrideLanguage(language), Times.Once);
        settings.Verify(service => service.TrySetAndSaveAsync(
            CaptureToolSettings.Settings_LanguageOverride,
            "fr-FR",
            TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task UpdateAppLanguageUseCase_WithDefaultLanguage_ClearsLanguageOverride()
    {
        var localization = new Mock<ILocalizationService>();
        Mock<ISettingsService> settings = CreatePersistingSettings();
        localization.Setup(service => service.SupportedLanguages).Returns([Mock.Of<IAppLanguage>()]);
        var useCase = new UpdateAppLanguageUseCase(localization.Object, settings.Object, TestUseCaseExecutor.Instance);

        UpdateAppLanguageResponse response = (await useCase.ExecuteAsync(new UpdateAppLanguageRequest(1), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        localization.Verify(service => service.OverrideLanguage(null), Times.Once);
        settings.Verify(service => service.TryUnsetAndSaveAsync(
            CaptureToolSettings.Settings_LanguageOverride,
            TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task UpdateAppLanguageUseCase_WhenPersistenceFails_DoesNotOverrideLanguage()
    {
        var language = Mock.Of<IAppLanguage>(appLanguage => appLanguage.Value == "fr-FR");
        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.SupportedLanguages).Returns([language]);
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.TrySetAndSaveAsync(
                CaptureToolSettings.Settings_LanguageOverride,
                "fr-FR",
                TestContext.CancellationToken))
            .ReturnsAsync(SettingsMutationResult.PersistenceFailed);
        var useCase = new UpdateAppLanguageUseCase(
            localization.Object,
            settings.Object,
            TestUseCaseExecutor.Instance);

        UpdateAppLanguageResponse response = (await useCase.ExecuteAsync(
            new UpdateAppLanguageRequest(0),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        localization.Verify(service => service.OverrideLanguage(It.IsAny<IAppLanguage?>()), Times.Never);
    }

    [TestMethod]
    public async Task UpdateAppLanguageUseCase_WithInvalidIndex_ReturnsFailureWithoutSaving()
    {
        var localization = new Mock<ILocalizationService>();
        Mock<ISettingsService> settings = CreatePersistingSettings();
        localization.Setup(service => service.SupportedLanguages).Returns([]);
        var useCase = new UpdateAppLanguageUseCase(localization.Object, settings.Object, TestUseCaseExecutor.Instance);

        Assert.IsFalse(useCase.CanExecute(new UpdateAppLanguageRequest(-1)));
        UpdateAppLanguageResponse response = (await useCase.ExecuteAsync(new UpdateAppLanguageRequest(-1), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        settings.Verify(service => service.TrySetAndSaveAsync(
            It.IsAny<IStringSettingDefinition>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        settings.Verify(service => service.TryUnsetAndSaveAsync(
            It.IsAny<ISettingDefinition>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task UpdateBooleanSettingsUseCases_SetExpectedSettingAndSave()
    {
        Mock<ISettingsService> settings = CreatePersistingSettings();

        await new UpdateImageAutoCopyUseCase(settings.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new UpdateImageAutoCopyRequest(false), TestContext.CancellationToken);
        await new UpdateImageAutoSaveUseCase(settings.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new UpdateImageAutoSaveRequest(true), TestContext.CancellationToken);
        await new UpdateVideoCaptureAutoCopyUseCase(settings.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new UpdateVideoCaptureAutoCopyRequest(false), TestContext.CancellationToken);
        await new UpdateVideoCaptureAutoSaveUseCase(settings.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new UpdateVideoCaptureAutoSaveRequest(true), TestContext.CancellationToken);
        await new UpdateVideoCaptureDefaultLocalAudioUseCase(settings.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new UpdateVideoCaptureDefaultLocalAudioRequest(false), TestContext.CancellationToken);
        await new UpdateAudioCaptureAutoCopyUseCase(settings.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new UpdateAudioCaptureAutoCopyRequest(false), TestContext.CancellationToken);
        await new UpdateAudioCaptureAutoSaveUseCase(settings.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new UpdateAudioCaptureAutoSaveRequest(true), TestContext.CancellationToken);
        await new UpdateAudioCaptureDefaultLocalAudioUseCase(settings.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new UpdateAudioCaptureDefaultLocalAudioRequest(false), TestContext.CancellationToken);
        await new UpdateCaptureWarnBeforeDiscardUseCase(settings.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new UpdateCaptureWarnBeforeDiscardRequest(false), TestContext.CancellationToken);
        var updateEditWarnBeforeDiscard =
            new UpdateEditWarnBeforeDiscardUseCase(settings.Object, TestUseCaseExecutor.Instance);
        Assert.IsTrue(updateEditWarnBeforeDiscard.CanExecute(new UpdateEditWarnBeforeDiscardRequest(true)));
        await updateEditWarnBeforeDiscard.ExecuteAsync(
            new UpdateEditWarnBeforeDiscardRequest(true),
            TestContext.CancellationToken);

        settings.Verify(service => service.TrySetAndSaveAsync(
            It.IsAny<IBoolSettingDefinition>(),
            It.IsAny<bool>(),
            TestContext.CancellationToken), Times.Exactly(10));
    }

    [TestMethod]
    public async Task UpdateImageAutoSaveUseCase_WhenPersistenceFails_ReturnsFailure()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.TrySetAndSaveAsync(
                CaptureToolSettings.Settings_ImageCapture_AutoSave,
                true,
                TestContext.CancellationToken))
            .ReturnsAsync(SettingsMutationResult.PersistenceFailed);
        var useCase = new UpdateImageAutoSaveUseCase(
            settings.Object,
            TestUseCaseExecutor.Instance);

        UpdateImageAutoSaveResponse response = (await useCase.ExecuteAsync(
            new UpdateImageAutoSaveRequest(true),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    private static Mock<ISettingsService> CreatePersistingSettings()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.TrySetAndSaveAsync(
                It.IsAny<IBoolSettingDefinition>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SettingsMutationResult.Saved);
        settings
            .Setup(service => service.TrySetAndSaveAsync(
                It.IsAny<IStringSettingDefinition>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SettingsMutationResult.Saved);
        settings
            .Setup(service => service.TryUnsetAndSaveAsync(
                It.IsAny<ISettingDefinition>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SettingsMutationResult.Saved);
        settings
            .Setup(service => service.TryClearAllAndSaveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SettingsMutationResult.Saved);
        return settings;
    }

    public TestContext TestContext { get; set; } = null!;
}
