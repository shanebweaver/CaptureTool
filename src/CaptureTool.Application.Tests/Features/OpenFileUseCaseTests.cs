using CaptureTool.Application.Abstractions.Features.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.AudioCapture;
using CaptureTool.Application.Features.AppMenu.OpenFile;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public class OpenFileUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithImageFile_ShouldNavigateToImageEdit()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IStorageService> storageService = new();
        string tempFolder = CreateTestFolder();
        string sourceFolder = CreateTestFolder();
        string sourcePath = Path.Combine(sourceFolder, "source.png");
        string copiedPath = Path.Combine(tempFolder, "source.png");
        await File.WriteAllTextAsync(sourcePath, "image", TestContext.CancellationToken);
        DateTime oldLastWriteTimeUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, oldLastWriteTimeUtc);

        filePickerService
            .Setup(service => service.PickFileAsync(FilePickerType.ImageOrVideo, UserFolder.Pictures))
            .ReturnsAsync(new FileReference(sourcePath));
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            navigationService.Object,
            storageService.Object,
            TestFileSystem.Instance,
            TestClock.Instance,
            TestUseCaseExecutor.Instance,
            new AllowAudioCaptureNavigationGuard());

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(copiedPath));
        Assert.IsTrue(File.GetLastWriteTimeUtc(copiedPath) > oldLastWriteTimeUtc);
        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.ImageEdit,
                It.Is<ImageFile>(file => file.FilePath == copiedPath)),
            Times.Once);
        storageService.Verify(service => service.GetTemporaryFileName(), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithVideoFile_ShouldNavigateToVideoEdit()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IStorageService> storageService = new();
        string tempFolder = CreateTestFolder();
        string sourceFolder = CreateTestFolder();
        string sourcePath = Path.Combine(sourceFolder, "source.mp4");
        string copiedPath = Path.Combine(tempFolder, "source.mp4");
        await File.WriteAllTextAsync(sourcePath, "video", TestContext.CancellationToken);
        DateTime oldLastWriteTimeUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, oldLastWriteTimeUtc);

        filePickerService
            .Setup(service => service.PickFileAsync(FilePickerType.ImageOrVideo, UserFolder.Pictures))
            .ReturnsAsync(new FileReference(sourcePath));
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            navigationService.Object,
            storageService.Object,
            TestFileSystem.Instance,
            TestClock.Instance,
            TestUseCaseExecutor.Instance,
            new AllowAudioCaptureNavigationGuard());

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(copiedPath));
        Assert.IsTrue(File.GetLastWriteTimeUtc(copiedPath) > oldLastWriteTimeUtc);
        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.VideoEdit,
                It.Is<VideoFile>(file => file.FilePath == copiedPath)),
            Times.Once);
        storageService.Verify(service => service.GetTemporaryFileName(), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithFileAlreadyInTemporaryFolder_ShouldNavigateToExistingFile()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IStorageService> storageService = new();
        string tempFolder = CreateTestFolder();
        string sourcePath = Path.Combine(tempFolder, "source.png");
        await File.WriteAllTextAsync(sourcePath, "image", TestContext.CancellationToken);
        DateTime oldLastWriteTimeUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, oldLastWriteTimeUtc);

        filePickerService
            .Setup(service => service.PickFileAsync(FilePickerType.ImageOrVideo, UserFolder.Pictures))
            .ReturnsAsync(new FileReference(sourcePath));
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            navigationService.Object,
            storageService.Object,
            TestFileSystem.Instance,
            TestClock.Instance,
            TestUseCaseExecutor.Instance,
            new AllowAudioCaptureNavigationGuard());

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.IsTrue(File.GetLastWriteTimeUtc(sourcePath) > oldLastWriteTimeUtc);
        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.ImageEdit,
                It.Is<ImageFile>(file => file.FilePath == sourcePath)),
            Times.Once);
        storageService.Verify(service => service.GetTemporaryFileName(), Times.Never);
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    public TestContext TestContext { get; set; } = null!;
}
