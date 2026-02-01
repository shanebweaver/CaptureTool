using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Application.Implementations.UseCases.VideoEdit;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Windowing;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.VideoEdit;

[TestClass]
public class VideoEditSaveUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldPickFileAndCopy()
    {
        var filePickerService = Fixture.Freeze<Mock<IFilePickerService>>();
        var windowingService = Fixture.Freeze<Mock<IWindowHandleProvider>>();
        windowingService.Setup(w => w.GetMainWindowHandle()).Returns(new nint(123));

        // Create both temp files
        var tempInputFile = Path.GetTempFileName();
        var tempOutputFile = Path.GetTempFileName();

        try
        {
            var mockFile = Mock.Of<IFile>(f => f.FilePath == tempOutputFile);

            filePickerService.Setup(f => f.PickSaveFileAsync(
                It.IsAny<nint>(),
                FilePickerType.Video,
                UserFolder.Videos))
                .ReturnsAsync(mockFile);

            var action = Fixture.Create<VideoEditSaveUseCase>();
            await action.ExecuteAsync(tempInputFile);

            filePickerService.Verify(f => f.PickSaveFileAsync(
                It.IsAny<nint>(),
                FilePickerType.Video,
                UserFolder.Videos), Times.Once);
        }
        finally
        {
            if (File.Exists(tempInputFile))
                File.Delete(tempInputFile);
            if (File.Exists(tempOutputFile))
                File.Delete(tempOutputFile);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldThrowWhenVideoPathIsEmpty()
    {
        var action = Fixture.Create<VideoEditSaveUseCase>();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => action.ExecuteAsync(string.Empty));
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldCopyMetadataFile_WhenFeatureEnabledAndSettingEnabled()
    {
        // Arrange
        var filePickerService = Fixture.Freeze<Mock<IFilePickerService>>();
        var windowingService = Fixture.Freeze<Mock<IWindowHandleProvider>>();
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        var settingsService = Fixture.Freeze<Mock<ISettingsService>>();

        windowingService.Setup(w => w.GetMainWindowHandle()).Returns(new nint(123));
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MetadataCollection)).Returns(true);
        settingsService.Setup(s => s.Get(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave)).Returns(true);

        var tempInputFile = Path.GetTempFileName();
        var tempOutputFile = Path.GetTempFileName();
        var tempMetadataInput = Path.ChangeExtension(tempInputFile, MetadataFile.FileExtension);
        var tempMetadataOutput = Path.ChangeExtension(tempOutputFile, MetadataFile.FileExtension);

        try
        {
            // Create metadata file
            File.WriteAllText(tempMetadataInput, "{}");

            var mockFile = Mock.Of<IFile>(f => f.FilePath == tempOutputFile);
            filePickerService.Setup(f => f.PickSaveFileAsync(
                It.IsAny<nint>(),
                FilePickerType.Video,
                UserFolder.Videos))
                .ReturnsAsync(mockFile);

            var action = Fixture.Create<VideoEditSaveUseCase>();

            // Act
            await action.ExecuteAsync(tempInputFile);

            // Assert
            Assert.IsTrue(File.Exists(tempMetadataOutput), "Metadata file should be copied");
        }
        finally
        {
            if (File.Exists(tempInputFile)) File.Delete(tempInputFile);
            if (File.Exists(tempOutputFile)) File.Delete(tempOutputFile);
            if (File.Exists(tempMetadataInput)) File.Delete(tempMetadataInput);
            if (File.Exists(tempMetadataOutput)) File.Delete(tempMetadataOutput);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldNotCopyMetadataFile_WhenFeatureDisabled()
    {
        // Arrange
        var filePickerService = Fixture.Freeze<Mock<IFilePickerService>>();
        var windowingService = Fixture.Freeze<Mock<IWindowHandleProvider>>();
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        var settingsService = Fixture.Freeze<Mock<ISettingsService>>();

        windowingService.Setup(w => w.GetMainWindowHandle()).Returns(new nint(123));
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MetadataCollection)).Returns(false);
        settingsService.Setup(s => s.Get(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave)).Returns(true);

        var tempInputFile = Path.GetTempFileName();
        var tempOutputFile = Path.GetTempFileName();
        var tempMetadataInput = Path.ChangeExtension(tempInputFile, MetadataFile.FileExtension);
        var tempMetadataOutput = Path.ChangeExtension(tempOutputFile, MetadataFile.FileExtension);

        try
        {
            // Create metadata file
            File.WriteAllText(tempMetadataInput, "{}");

            var mockFile = Mock.Of<IFile>(f => f.FilePath == tempOutputFile);
            filePickerService.Setup(f => f.PickSaveFileAsync(
                It.IsAny<nint>(),
                FilePickerType.Video,
                UserFolder.Videos))
                .ReturnsAsync(mockFile);

            var action = Fixture.Create<VideoEditSaveUseCase>();

            // Act
            await action.ExecuteAsync(tempInputFile);

            // Assert
            Assert.IsFalse(File.Exists(tempMetadataOutput), "Metadata file should not be copied when feature is disabled");
        }
        finally
        {
            if (File.Exists(tempInputFile)) File.Delete(tempInputFile);
            if (File.Exists(tempOutputFile)) File.Delete(tempOutputFile);
            if (File.Exists(tempMetadataInput)) File.Delete(tempMetadataInput);
            if (File.Exists(tempMetadataOutput)) File.Delete(tempMetadataOutput);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldNotCopyMetadataFile_WhenSettingDisabled()
    {
        // Arrange
        var filePickerService = Fixture.Freeze<Mock<IFilePickerService>>();
        var windowingService = Fixture.Freeze<Mock<IWindowHandleProvider>>();
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        var settingsService = Fixture.Freeze<Mock<ISettingsService>>();

        windowingService.Setup(w => w.GetMainWindowHandle()).Returns(new nint(123));
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MetadataCollection)).Returns(true);
        settingsService.Setup(s => s.Get(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave)).Returns(false);

        var tempInputFile = Path.GetTempFileName();
        var tempOutputFile = Path.GetTempFileName();
        var tempMetadataInput = Path.ChangeExtension(tempInputFile, MetadataFile.FileExtension);
        var tempMetadataOutput = Path.ChangeExtension(tempOutputFile, MetadataFile.FileExtension);

        try
        {
            // Create metadata file
            File.WriteAllText(tempMetadataInput, "{}");

            var mockFile = Mock.Of<IFile>(f => f.FilePath == tempOutputFile);
            filePickerService.Setup(f => f.PickSaveFileAsync(
                It.IsAny<nint>(),
                FilePickerType.Video,
                UserFolder.Videos))
                .ReturnsAsync(mockFile);

            var action = Fixture.Create<VideoEditSaveUseCase>();

            // Act
            await action.ExecuteAsync(tempInputFile);

            // Assert
            Assert.IsFalse(File.Exists(tempMetadataOutput), "Metadata file should not be copied when setting is disabled");
        }
        finally
        {
            if (File.Exists(tempInputFile)) File.Delete(tempInputFile);
            if (File.Exists(tempOutputFile)) File.Delete(tempOutputFile);
            if (File.Exists(tempMetadataInput)) File.Delete(tempMetadataInput);
            if (File.Exists(tempMetadataOutput)) File.Delete(tempMetadataOutput);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldNotThrow_WhenMetadataFileDoesNotExist()
    {
        // Arrange
        var filePickerService = Fixture.Freeze<Mock<IFilePickerService>>();
        var windowingService = Fixture.Freeze<Mock<IWindowHandleProvider>>();
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        var settingsService = Fixture.Freeze<Mock<ISettingsService>>();

        windowingService.Setup(w => w.GetMainWindowHandle()).Returns(new nint(123));
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MetadataCollection)).Returns(true);
        settingsService.Setup(s => s.Get(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave)).Returns(true);

        var tempInputFile = Path.GetTempFileName();
        var tempOutputFile = Path.GetTempFileName();
        var tempMetadataOutput = Path.ChangeExtension(tempOutputFile, MetadataFile.FileExtension);

        try
        {
            // Do NOT create metadata file

            var mockFile = Mock.Of<IFile>(f => f.FilePath == tempOutputFile);
            filePickerService.Setup(f => f.PickSaveFileAsync(
                It.IsAny<nint>(),
                FilePickerType.Video,
                UserFolder.Videos))
                .ReturnsAsync(mockFile);

            var action = Fixture.Create<VideoEditSaveUseCase>();

            // Act - should not throw
            await action.ExecuteAsync(tempInputFile);

            // Assert
            Assert.IsFalse(File.Exists(tempMetadataOutput), "Metadata file should not be copied when it doesn't exist");
        }
        finally
        {
            if (File.Exists(tempInputFile)) File.Delete(tempInputFile);
            if (File.Exists(tempOutputFile)) File.Delete(tempOutputFile);
            if (File.Exists(tempMetadataOutput)) File.Delete(tempMetadataOutput);
        }
    }
}
