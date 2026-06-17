using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Abstractions.Features.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Features.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Features.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.Features.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Features.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Features.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Features.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Features.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Application.Features.SettingsPage.ChangeScreenshotsFolder;
using CaptureTool.Application.Features.SettingsPage.ChangeVideosFolder;
using CaptureTool.Application.Features.SettingsPage.ClearTempFiles;
using CaptureTool.Application.Features.SettingsPage.LeaveSettingsPage;
using CaptureTool.Application.Features.SettingsPage.OpenScreenshotsFolder;
using CaptureTool.Application.Features.SettingsPage.OpenTempFolder;
using CaptureTool.Application.Features.SettingsPage.OpenVideosFolder;
using CaptureTool.Application.Features.SettingsPage.RestartSettingsApplication;
using CaptureTool.Application.Features.SettingsPage.RestoreDefaults;
using CaptureTool.Application.Features.SettingsPage.UpdateAppLanguage;
using CaptureTool.Application.Features.SettingsPage.UpdateImageAutoCopy;
using CaptureTool.Application.Features.SettingsPage.UpdateImageAutoSave;
using CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureDefaultLocalAudio;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class SettingsPageUseCaseTests
{
    [TestMethod]
    public async Task ChangeScreenshotsFolderUseCase_WhenFolderSelected_SavesFolderSetting()
    {
        var picker = new Mock<IFilePickerService>();
        var settings = new Mock<ISettingsService>();
        picker
            .Setup(service => service.PickFolderAsync(UserFolder.Pictures))
            .ReturnsAsync(Mock.Of<IFolder>(folder => folder.FolderPath == @"C:\Screenshots"));
        var useCase = new ChangeScreenshotsFolderUseCase(picker.Object, settings.Object, TestUseCaseExecutor.Instance);

        ChangeScreenshotsFolderResponse response = (await useCase.ExecuteAsync(new ChangeScreenshotsFolderRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Changed);
        settings.Verify(service => service.Set(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder, @"C:\Screenshots"), Times.Once);
        settings.Verify(service => service.TrySaveAsync(TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task ChangeVideosFolderUseCase_WhenPickerCanceled_DoesNotSaveSetting()
    {
        var picker = new Mock<IFilePickerService>();
        var settings = new Mock<ISettingsService>();
        picker
            .Setup(service => service.PickFolderAsync(UserFolder.Videos))
            .ReturnsAsync((IFolder?)null);
        var useCase = new ChangeVideosFolderUseCase(picker.Object, settings.Object, TestUseCaseExecutor.Instance);

        ChangeVideosFolderResponse response = (await useCase.ExecuteAsync(new ChangeVideosFolderRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Changed);
        settings.Verify(service => service.Set(It.IsAny<IStringSettingDefinition>(), It.IsAny<string>()), Times.Never);
        settings.Verify(service => service.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        var useCase = new ClearTempFilesUseCase(Mock.Of<ILogService>(), storage.Object, TestUseCaseExecutor.Instance);

        ClearTempFilesResponse response = (await useCase.ExecuteAsync(new ClearTempFilesRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(tempFolder).ToArray());
    }

    [TestMethod]
    public async Task LeaveSettingsPageUseCase_WhenCannotGoBack_NavigatesHomeAndClearsHistory()
    {
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.TryGoBack()).Returns(false);
        var useCase = new LeaveSettingsPageUseCase(navigation.Object, TestUseCaseExecutor.Instance);

        LeaveSettingsPageResponse response = (await useCase.ExecuteAsync(new LeaveSettingsPageRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        navigation.Verify(service => service.Navigate(NavigationRoute.Home, null, true), Times.Once);
    }

    [TestMethod]
    public async Task OpenFolderUseCases_WhenFoldersAreMissing_ReturnNotOpened()
    {
        string missingFolder = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        var settings = new Mock<ISettingsService>();
        var storage = new Mock<IStorageService>();
        settings.Setup(service => service.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder)).Returns("");
        settings.Setup(service => service.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder)).Returns("");
        storage.Setup(service => service.GetSystemDefaultScreenshotsFolderPath()).Returns(missingFolder);
        storage.Setup(service => service.GetSystemDefaultVideosFolderPath()).Returns(missingFolder);
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(missingFolder);

        var screenshots = new OpenScreenshotsFolderUseCase(settings.Object, storage.Object, TestUseCaseExecutor.Instance);
        var videos = new OpenVideosFolderUseCase(settings.Object, storage.Object, TestUseCaseExecutor.Instance);
        var temp = new OpenTempFolderUseCase(storage.Object, TestUseCaseExecutor.Instance);

        OpenScreenshotsFolderResponse screenshotsResponse = (await screenshots.ExecuteAsync(new OpenScreenshotsFolderRequest(), TestContext.CancellationToken)).Value!;
        OpenVideosFolderResponse videosResponse = (await videos.ExecuteAsync(new OpenVideosFolderRequest(), TestContext.CancellationToken)).Value!;
        OpenTempFolderResponse tempResponse = (await temp.ExecuteAsync(new OpenTempFolderRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(screenshotsResponse.Opened);
        Assert.IsFalse(videosResponse.Opened);
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
        var settings = new Mock<ISettingsService>();
        var localization = new Mock<ILocalizationService>();
        var useCase = new RestoreDefaultsUseCase(settings.Object, localization.Object, TestUseCaseExecutor.Instance);

        RestoreDefaultsResponse response = (await useCase.ExecuteAsync(new RestoreDefaultsRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        settings.Verify(service => service.ClearAllSettings(), Times.Once);
        localization.Verify(service => service.OverrideLanguage(null), Times.Once);
        settings.Verify(service => service.TrySaveAsync(TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task UpdateAppLanguageUseCase_WithSupportedLanguage_OverridesLanguageAndSavesSetting()
    {
        var language = Mock.Of<IAppLanguage>(appLanguage => appLanguage.Value == "fr-FR");
        var localization = new Mock<ILocalizationService>();
        var settings = new Mock<ISettingsService>();
        localization.Setup(service => service.SupportedLanguages).Returns([language]);
        var useCase = new UpdateAppLanguageUseCase(localization.Object, settings.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new UpdateAppLanguageRequest(0)));
        UpdateAppLanguageResponse response = (await useCase.ExecuteAsync(new UpdateAppLanguageRequest(0), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        localization.Verify(service => service.OverrideLanguage(language), Times.Once);
        settings.Verify(service => service.Set(CaptureToolSettings.Settings_LanguageOverride, "fr-FR"), Times.Once);
        settings.Verify(service => service.TrySaveAsync(TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task UpdateAppLanguageUseCase_WithDefaultLanguage_ClearsLanguageOverride()
    {
        var localization = new Mock<ILocalizationService>();
        var settings = new Mock<ISettingsService>();
        localization.Setup(service => service.SupportedLanguages).Returns([Mock.Of<IAppLanguage>()]);
        var useCase = new UpdateAppLanguageUseCase(localization.Object, settings.Object, TestUseCaseExecutor.Instance);

        UpdateAppLanguageResponse response = (await useCase.ExecuteAsync(new UpdateAppLanguageRequest(1), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        localization.Verify(service => service.OverrideLanguage(null), Times.Once);
        settings.Verify(service => service.Unset(CaptureToolSettings.Settings_LanguageOverride), Times.Once);
    }

    [TestMethod]
    public async Task UpdateAppLanguageUseCase_WithInvalidIndex_ReturnsFailureWithoutSaving()
    {
        var localization = new Mock<ILocalizationService>();
        var settings = new Mock<ISettingsService>();
        localization.Setup(service => service.SupportedLanguages).Returns([]);
        var useCase = new UpdateAppLanguageUseCase(localization.Object, settings.Object, TestUseCaseExecutor.Instance);

        Assert.IsFalse(useCase.CanExecute(new UpdateAppLanguageRequest(-1)));
        UpdateAppLanguageResponse response = (await useCase.ExecuteAsync(new UpdateAppLanguageRequest(-1), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        settings.Verify(service => service.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task UpdateBooleanSettingsUseCases_SetExpectedSettingAndSave()
    {
        var settings = new Mock<ISettingsService>();

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

        settings.Verify(service => service.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, false), Times.Once);
        settings.Verify(service => service.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, true), Times.Once);
        settings.Verify(service => service.Set(CaptureToolSettings.Settings_VideoCapture_AutoCopy, false), Times.Once);
        settings.Verify(service => service.Set(CaptureToolSettings.Settings_VideoCapture_AutoSave, true), Times.Once);
        settings.Verify(service => service.Set(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled, false), Times.Once);
        settings.Verify(service => service.TrySaveAsync(TestContext.CancellationToken), Times.Exactly(5));
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    public TestContext TestContext { get; set; } = null!;
}
