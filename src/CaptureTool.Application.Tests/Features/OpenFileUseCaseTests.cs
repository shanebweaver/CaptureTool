using CaptureTool.Application.Abstractions.Features.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Application.Features.AppMenu.OpenFile;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public class OpenFileUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithImageFile_ShouldNavigateToImageEdit()
    {
        Mock<IFileTypeDetector> fileTypeDetector = new();
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IStorageService> storageService = new();
        Mock<IWindowHandleProvider> windowHandleProvider = new();
        string tempFolder = CreateTestFolder();
        string sourcePath = Path.Combine(tempFolder, "source.png");
        string copiedPath = Path.Combine(tempFolder, "opened.png");
        await File.WriteAllTextAsync(sourcePath, "image");

        filePickerService
            .Setup(service => service.PickFileAsync(It.IsAny<nint>(), FilePickerType.ImageOrVideo, UserFolder.Pictures))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == sourcePath));
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        storageService
            .Setup(service => service.GetTemporaryFileName())
            .Returns("opened.tmp");
        fileTypeDetector
            .Setup(detector => detector.DetectFileType(copiedPath))
            .Returns(CaptureFileType.Image);

        OpenFileUseCase useCase = new(
            fileTypeDetector.Object,
            filePickerService.Object,
            navigationService.Object,
            storageService.Object,
            windowHandleProvider.Object);

        await useCase.ExecuteAsync(new OpenFileRequest());

        Assert.IsTrue(File.Exists(copiedPath));
        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.ImageEdit,
                It.Is<ImageFile>(file => file.FilePath == copiedPath)),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithVideoFile_ShouldNavigateToVideoEdit()
    {
        Mock<IFileTypeDetector> fileTypeDetector = new();
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IStorageService> storageService = new();
        Mock<IWindowHandleProvider> windowHandleProvider = new();
        string tempFolder = CreateTestFolder();
        string sourcePath = Path.Combine(tempFolder, "source.mp4");
        string copiedPath = Path.Combine(tempFolder, "opened.mp4");
        await File.WriteAllTextAsync(sourcePath, "video");

        filePickerService
            .Setup(service => service.PickFileAsync(It.IsAny<nint>(), FilePickerType.ImageOrVideo, UserFolder.Pictures))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == sourcePath));
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        storageService
            .Setup(service => service.GetTemporaryFileName())
            .Returns("opened.tmp");
        fileTypeDetector
            .Setup(detector => detector.DetectFileType(copiedPath))
            .Returns(CaptureFileType.Video);

        OpenFileUseCase useCase = new(
            fileTypeDetector.Object,
            filePickerService.Object,
            navigationService.Object,
            storageService.Object,
            windowHandleProvider.Object);

        await useCase.ExecuteAsync(new OpenFileRequest());

        Assert.IsTrue(File.Exists(copiedPath));
        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.VideoEdit,
                It.Is<VideoFile>(file => file.FilePath == copiedPath)),
            Times.Once);
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }
}
