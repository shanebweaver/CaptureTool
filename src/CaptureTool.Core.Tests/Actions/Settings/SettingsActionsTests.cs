using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Settings;
using CaptureTool.Core.Interfaces.Actions.Settings;
using Moq;

namespace CaptureTool.Core.Tests.Actions.Settings;

[TestClass]
public class SettingsActionsTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void GoBack_ShouldDelegateToAction()
    {
        var goBack = Fixture.Freeze<Mock<ISettingsGoBackAction>>();
        var restart = Fixture.Freeze<Mock<ISettingsRestartAppAction>>();
        var updateCopy = Fixture.Freeze<Mock<ISettingsUpdateImageAutoCopyAction>>();
        var updateSave = Fixture.Freeze<Mock<ISettingsUpdateImageAutoSaveAction>>();
        var updateVideoCopy = Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoCopyAction>>();
        var updateVideoSave = Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoSaveAction>>();
        var updateLang = Fixture.Freeze<Mock<ISettingsUpdateAppLanguageAction>>();
        var updateTheme = Fixture.Freeze<Mock<ISettingsUpdateAppThemeAction>>();
        var changeFolder = Fixture.Freeze<Mock<ISettingsChangeScreenshotsFolderAction>>();
        var openShots = Fixture.Freeze<Mock<ISettingsOpenScreenshotsFolderAction>>();
        var changeVideos = Fixture.Freeze<Mock<ISettingsChangeVideosFolderAction>>();
        var openVideos = Fixture.Freeze<Mock<ISettingsOpenVideosFolderAction>>();
        var openTemp = Fixture.Freeze<Mock<ISettingsOpenTempFolderAction>>();
        var clearTemp = Fixture.Freeze<Mock<ISettingsClearTempFilesAction>>();
        var restore = Fixture.Freeze<Mock<ISettingsRestoreDefaultsAction>>();

        goBack.Setup(a => a.CanExecute()).Returns(true);

        var actions = new SettingsActions(
            goBack.Object,
            restart.Object,
            updateCopy.Object,
            updateSave.Object,
            updateVideoCopy.Object,
            updateVideoSave.Object,
            updateLang.Object,
            updateTheme.Object,
            changeFolder.Object,
            openShots.Object,
            changeVideos.Object,
            openVideos.Object,
            openTemp.Object,
            clearTemp.Object,
            restore.Object);

        actions.GoBack();

        goBack.Verify(a => a.CanExecute(), Times.Once);
        goBack.Verify(a => a.Execute(), Times.Once);
    }

    [TestMethod]
    public void RestartApp_ShouldDelegateToAction()
    {
        var goBack = Fixture.Freeze<Mock<ISettingsGoBackAction>>();
        var restart = Fixture.Freeze<Mock<ISettingsRestartAppAction>>();
        var updateCopy = Fixture.Freeze<Mock<ISettingsUpdateImageAutoCopyAction>>();
        var updateSave = Fixture.Freeze<Mock<ISettingsUpdateImageAutoSaveAction>>();
        var updateVideoCopy = Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoCopyAction>>();
        var updateVideoSave = Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoSaveAction>>();
        var updateLang = Fixture.Freeze<Mock<ISettingsUpdateAppLanguageAction>>();
        var updateTheme = Fixture.Freeze<Mock<ISettingsUpdateAppThemeAction>>();
        var changeFolder = Fixture.Freeze<Mock<ISettingsChangeScreenshotsFolderAction>>();
        var openShots = Fixture.Freeze<Mock<ISettingsOpenScreenshotsFolderAction>>();
        var changeVideos = Fixture.Freeze<Mock<ISettingsChangeVideosFolderAction>>();
        var openVideos = Fixture.Freeze<Mock<ISettingsOpenVideosFolderAction>>();
        var openTemp = Fixture.Freeze<Mock<ISettingsOpenTempFolderAction>>();
        var clearTemp = Fixture.Freeze<Mock<ISettingsClearTempFilesAction>>();
        var restore = Fixture.Freeze<Mock<ISettingsRestoreDefaultsAction>>();

        restart.Setup(a => a.CanExecute()).Returns(true);

        var actions = new SettingsActions(
            goBack.Object,
            restart.Object,
            updateCopy.Object,
            updateSave.Object,
            updateVideoCopy.Object,
            updateVideoSave.Object,
            updateLang.Object,
            updateTheme.Object,
            changeFolder.Object,
            openShots.Object,
            changeVideos.Object,
            openVideos.Object,
            openTemp.Object,
            clearTemp.Object,
            restore.Object);

        actions.RestartApp();

        restart.Verify(a => a.CanExecute(), Times.Once);
        restart.Verify(a => a.Execute(), Times.Once);
    }

    [TestMethod]
    public void ClearTemporaryFiles_ShouldDelegateToAction_WithProvidedPath()
    {
        var goBack = Fixture.Freeze<Mock<ISettingsGoBackAction>>();
        var restart = Fixture.Freeze<Mock<ISettingsRestartAppAction>>();
        var updateCopy = Fixture.Freeze<Mock<ISettingsUpdateImageAutoCopyAction>>();
        var updateSave = Fixture.Freeze<Mock<ISettingsUpdateImageAutoSaveAction>>();
        var updateVideoCopy = Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoCopyAction>>();
        var updateVideoSave = Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoSaveAction>>();
        var updateLang = Fixture.Freeze<Mock<ISettingsUpdateAppLanguageAction>>();
        var updateTheme = Fixture.Freeze<Mock<ISettingsUpdateAppThemeAction>>();
        var changeFolder = Fixture.Freeze<Mock<ISettingsChangeScreenshotsFolderAction>>();
        var openShots = Fixture.Freeze<Mock<ISettingsOpenScreenshotsFolderAction>>();
        var changeVideos = Fixture.Freeze<Mock<ISettingsChangeVideosFolderAction>>();
        var openVideos = Fixture.Freeze<Mock<ISettingsOpenVideosFolderAction>>();
        var openTemp = Fixture.Freeze<Mock<ISettingsOpenTempFolderAction>>();
        var clearTemp = Fixture.Freeze<Mock<ISettingsClearTempFilesAction>>();
        var restore = Fixture.Freeze<Mock<ISettingsRestoreDefaultsAction>>();

        var tempPath = Fixture.Create<string>();
        clearTemp.Setup(a => a.CanExecute(tempPath)).Returns(true);

        var actions = new SettingsActions(
            goBack.Object,
            restart.Object,
            updateCopy.Object,
            updateSave.Object,
            updateVideoCopy.Object,
            updateVideoSave.Object,
            updateLang.Object,
            updateTheme.Object,
            changeFolder.Object,
            openShots.Object,
            changeVideos.Object,
            openVideos.Object,
            openTemp.Object,
            clearTemp.Object,
            restore.Object);

        actions.ClearTemporaryFiles(tempPath);

        clearTemp.Verify(a => a.CanExecute(tempPath), Times.Once);
        clearTemp.Verify(a => a.Execute(tempPath), Times.Once);
    }

    [TestMethod]
    public async Task RestoreDefaultSettingsAsync_ShouldDelegateToAsyncAction()
    {
        var goBack = Fixture.Freeze<Mock<ISettingsGoBackAction>>();
        var restart = Fixture.Freeze<Mock<ISettingsRestartAppAction>>();
        var updateCopy = Fixture.Freeze<Mock<ISettingsUpdateImageAutoCopyAction>>();
        var updateSave = Fixture.Freeze<Mock<ISettingsUpdateImageAutoSaveAction>>();
        var updateVideoCopy = Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoCopyAction>>();
        var updateVideoSave = Fixture.Freeze<Mock<ISettingsUpdateVideoCaptureAutoSaveAction>>();
        var updateLang = Fixture.Freeze<Mock<ISettingsUpdateAppLanguageAction>>();
        var updateTheme = Fixture.Freeze<Mock<ISettingsUpdateAppThemeAction>>();
        var changeFolder = Fixture.Freeze<Mock<ISettingsChangeScreenshotsFolderAction>>();
        var openShots = Fixture.Freeze<Mock<ISettingsOpenScreenshotsFolderAction>>();
        var changeVideos = Fixture.Freeze<Mock<ISettingsChangeVideosFolderAction>>();
        var openVideos = Fixture.Freeze<Mock<ISettingsOpenVideosFolderAction>>();
        var openTemp = Fixture.Freeze<Mock<ISettingsOpenTempFolderAction>>();
        var clearTemp = Fixture.Freeze<Mock<ISettingsClearTempFilesAction>>();
        var restore = Fixture.Freeze<Mock<ISettingsRestoreDefaultsAction>>();

        restore.Setup(a => a.CanExecute()).Returns(true);
        restore.Setup(a => a.ExecuteAsync()).Returns(Task.CompletedTask);

        var actions = new SettingsActions(
            goBack.Object,
            restart.Object,
            updateCopy.Object,
            updateSave.Object,
            updateVideoCopy.Object,
            updateVideoSave.Object,
            updateLang.Object,
            updateTheme.Object,
            changeFolder.Object,
            openShots.Object,
            changeVideos.Object,
            openVideos.Object,
            openTemp.Object,
            clearTemp.Object,
            restore.Object);

        await actions.RestoreDefaultSettingsAsync(CancellationToken.None);

        restore.Verify(a => a.CanExecute(), Times.Once);
        restore.Verify(a => a.ExecuteAsync(), Times.Once);
    }
}
