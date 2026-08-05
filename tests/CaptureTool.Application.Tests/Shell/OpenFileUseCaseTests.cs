using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Shell.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Shell.AppMenu.OpenFile;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using Moq;

namespace CaptureTool.Application.Tests.Shell;

[TestClass]
public class OpenFileUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_WhenLeavePolicyRejects_DoesNotOpenPickerOrNavigate()
    {
        var filePickerService = new Mock<IFilePickerService>();
        var navigationService = new Mock<INavigationService>();
        var editSessionGuard = new Mock<IEditSessionGuard>();
        editSessionGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            TestNavigationCoordinator.Create(
                navigationService.Object,
                editSessionGuard.Object),
            Mock.Of<IStorageService>(),
            TestFileSystem.Instance,
            Mock.Of<IRecentCaptureCatalog>(),
            TestUseCaseExecutor.Instance);

        OpenFileResponse response = (await useCase.ExecuteAsync(
            new OpenFileRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Opened);
        filePickerService.Verify(
            service => service.PickFileAsync(It.IsAny<FilePickerType>(), It.IsAny<UserFolder>()),
            Times.Never);
        navigationService.Verify(
            service => service.Navigate(It.IsAny<object>(), It.IsAny<object?>(), It.IsAny<bool>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithImageFile_ShouldNavigateToImageEdit()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IStorageService> storageService = new();
        Mock<IRecentCaptureCatalog> recentCaptureCatalog = new();
        string tempFolder = CreateTestFolder();
        string sourceFolder = CreateTestFolder();
        string sourcePath = Path.Combine(sourceFolder, "source.png");
        string copiedPath = Path.Combine(tempFolder, "open-file.png");
        await File.WriteAllTextAsync(sourcePath, "image", TestContext.CancellationToken);
        DateTime oldLastWriteTimeUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, oldLastWriteTimeUtc);

        filePickerService
            .Setup(service => service.PickFileAsync(FilePickerType.CaptureMedia, UserFolder.Pictures))
            .ReturnsAsync(new FileReference(sourcePath));
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        storageService
            .Setup(service => service.GetTemporaryFileName())
            .Returns("open-file.tmp");
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            TestNavigationCoordinator.Create(navigationService.Object),
            storageService.Object,
            TestFileSystem.Instance,
            recentCaptureCatalog.Object,
            TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(copiedPath));
        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.ImageEdit,
                It.Is<ImageFile>(file =>
                    file.FilePath == copiedPath &&
                    file.PersistentFilePath == sourcePath)),
            Times.Once);
        storageService.Verify(service => service.GetTemporaryFileName(), Times.Once);
        recentCaptureCatalog.Verify(
            catalog => catalog.RecordOpened(sourcePath, CaptureFileType.Image),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithAudioFile_ShouldNavigateToAudioEdit()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IStorageService> storageService = new();
        Mock<IRecentCaptureCatalog> recentCaptureCatalog = new();
        string tempFolder = CreateTestFolder();
        string sourceFolder = CreateTestFolder();
        string sourcePath = Path.Combine(sourceFolder, "source.wav");
        string copiedPath = Path.Combine(tempFolder, "open-file.wav");
        await File.WriteAllTextAsync(sourcePath, "audio", TestContext.CancellationToken);
        DateTime oldLastWriteTimeUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, oldLastWriteTimeUtc);

        filePickerService
            .Setup(service => service.PickFileAsync(FilePickerType.CaptureMedia, UserFolder.Pictures))
            .ReturnsAsync(new FileReference(sourcePath));
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        storageService
            .Setup(service => service.GetTemporaryFileName())
            .Returns("open-file.tmp");
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            TestNavigationCoordinator.Create(navigationService.Object),
            storageService.Object,
            TestFileSystem.Instance,
            recentCaptureCatalog.Object,
            TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(copiedPath));
        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.AudioEdit,
                It.Is<AudioFile>(file => file.FilePath == copiedPath)),
            Times.Once);
        storageService.Verify(service => service.GetTemporaryFileName(), Times.Once);
        recentCaptureCatalog.Verify(
            catalog => catalog.RecordOpened(sourcePath, CaptureFileType.Audio),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithVideoFile_ShouldNavigateToVideoEdit()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IStorageService> storageService = new();
        Mock<IRecentCaptureCatalog> recentCaptureCatalog = new();
        string tempFolder = CreateTestFolder();
        string sourceFolder = CreateTestFolder();
        string sourcePath = Path.Combine(sourceFolder, "source.mp4");
        string copiedPath = Path.Combine(tempFolder, "open-file.mp4");
        await File.WriteAllTextAsync(sourcePath, "video", TestContext.CancellationToken);
        DateTime oldLastWriteTimeUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, oldLastWriteTimeUtc);

        filePickerService
            .Setup(service => service.PickFileAsync(FilePickerType.CaptureMedia, UserFolder.Pictures))
            .ReturnsAsync(new FileReference(sourcePath));
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        storageService
            .Setup(service => service.GetTemporaryFileName())
            .Returns("open-file.tmp");
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            TestNavigationCoordinator.Create(navigationService.Object),
            storageService.Object,
            TestFileSystem.Instance,
            recentCaptureCatalog.Object,
            TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(copiedPath));
        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.VideoEdit,
                It.Is<VideoFile>(file => file.FilePath == copiedPath)),
            Times.Once);
        storageService.Verify(service => service.GetTemporaryFileName(), Times.Once);
        recentCaptureCatalog.Verify(
            catalog => catalog.RecordOpened(sourcePath, CaptureFileType.Video),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithFileAlreadyInTemporaryFolder_ShouldNavigateToExistingFile()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IStorageService> storageService = new();
        Mock<IRecentCaptureCatalog> recentCaptureCatalog = new();
        string tempFolder = CreateTestFolder();
        string sourcePath = Path.Combine(tempFolder, "source.png");
        await File.WriteAllTextAsync(sourcePath, "image", TestContext.CancellationToken);
        DateTime oldLastWriteTimeUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, oldLastWriteTimeUtc);

        filePickerService
            .Setup(service => service.PickFileAsync(FilePickerType.CaptureMedia, UserFolder.Pictures))
            .ReturnsAsync(new FileReference(sourcePath));
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            TestNavigationCoordinator.Create(navigationService.Object),
            storageService.Object,
            TestFileSystem.Instance,
            recentCaptureCatalog.Object,
            TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.AreEqual(oldLastWriteTimeUtc, File.GetLastWriteTimeUtc(sourcePath));
        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.ImageEdit,
                It.Is<ImageFile>(file =>
                    file.FilePath == sourcePath &&
                    file.PersistentFilePath == null)),
            Times.Once);
        storageService.Verify(service => service.GetTemporaryFileName(), Times.Never);
        recentCaptureCatalog.Verify(
            catalog => catalog.RecordOpened(sourcePath, CaptureFileType.Image),
            Times.Once);
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    public TestContext TestContext { get; set; } = null!;
}
