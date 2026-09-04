using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;
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
            Mock.Of<IScratchArtifactStore>(),
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
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithImageFile_ShouldNavigateToImageEdit()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        TestNavigationService.AcceptAll(navigationService);
        Mock<IScratchArtifactStore> scratchArtifactStore = new();
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
        scratchArtifactStore
            .Setup(service => service.CreateLeasedArtifactPath("imported-working-copy", ".png"))
            .Returns(copiedPath);
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            TestNavigationCoordinator.Create(navigationService.Object),
            scratchArtifactStore.Object,
            TestFileSystem.Instance,
            recentCaptureCatalog.Object,
            TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(copiedPath));
        navigationService.Verify(
            service => service.NavigateAsync(
                NavigationRoute.ImageEdit,
                It.Is<OpenImageEditPageRequest>(request =>
                    request.ImageFile.FilePath == copiedPath &&
                    request.ImageFile.PersistentFilePath == sourcePath &&
                    request.EditorContext != null &&
                    request.EditorContext.PersistentSourcePath == sourcePath),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
        scratchArtifactStore.Verify(service => service.CreateLeasedArtifactPath("imported-working-copy", ".png"), Times.Once);
        recentCaptureCatalog.Verify(
            catalog => catalog.RecordOpened(sourcePath, CaptureFileType.Image),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithAudioFile_ShouldNavigateToAudioEdit()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        TestNavigationService.AcceptAll(navigationService);
        Mock<IScratchArtifactStore> scratchArtifactStore = new();
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
        scratchArtifactStore
            .Setup(service => service.CreateLeasedArtifactPath("imported-working-copy", ".wav"))
            .Returns(copiedPath);
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            TestNavigationCoordinator.Create(navigationService.Object),
            scratchArtifactStore.Object,
            TestFileSystem.Instance,
            recentCaptureCatalog.Object,
            TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(copiedPath));
        navigationService.Verify(
            service => service.NavigateAsync(
                NavigationRoute.AudioEdit,
                It.Is<OpenAudioEditPageRequest>(request =>
                    request.AudioFile.FilePath == copiedPath &&
                    request.EditorContext != null &&
                    request.EditorContext.PersistentSourcePath == sourcePath),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
        scratchArtifactStore.Verify(service => service.CreateLeasedArtifactPath("imported-working-copy", ".wav"), Times.Once);
        recentCaptureCatalog.Verify(
            catalog => catalog.RecordOpened(sourcePath, CaptureFileType.Audio),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithVideoFile_ShouldNavigateToVideoEdit()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        TestNavigationService.AcceptAll(navigationService);
        Mock<IScratchArtifactStore> scratchArtifactStore = new();
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
        scratchArtifactStore
            .Setup(service => service.CreateLeasedArtifactPath("imported-working-copy", ".mp4"))
            .Returns(copiedPath);
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            TestNavigationCoordinator.Create(navigationService.Object),
            scratchArtifactStore.Object,
            TestFileSystem.Instance,
            recentCaptureCatalog.Object,
            TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(copiedPath));
        navigationService.Verify(
            service => service.NavigateAsync(
                NavigationRoute.VideoEdit,
                It.Is<OpenVideoEditPageRequest>(request =>
                    request.VideoFile.FilePath == copiedPath &&
                    request.EditorContext != null &&
                    request.EditorContext.PersistentSourcePath == sourcePath),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
        scratchArtifactStore.Verify(service => service.CreateLeasedArtifactPath("imported-working-copy", ".mp4"), Times.Once);
        recentCaptureCatalog.Verify(
            catalog => catalog.RecordOpened(sourcePath, CaptureFileType.Video),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithFileAlreadyInScratchFolder_ShouldCreateOwnedWorkingCopy()
    {
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        TestNavigationService.AcceptAll(navigationService);
        Mock<IScratchArtifactStore> scratchArtifactStore = new();
        Mock<IRecentCaptureCatalog> recentCaptureCatalog = new();
        string tempFolder = CreateTestFolder();
        string sourcePath = Path.Combine(tempFolder, "source.png");
        string copiedPath = Path.Combine(tempFolder, "owned-copy.png");
        await File.WriteAllTextAsync(sourcePath, "image", TestContext.CancellationToken);
        DateTime oldLastWriteTimeUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, oldLastWriteTimeUtc);

        filePickerService
            .Setup(service => service.PickFileAsync(FilePickerType.CaptureMedia, UserFolder.Pictures))
            .ReturnsAsync(new FileReference(sourcePath));
        scratchArtifactStore
            .Setup(service => service.CreateLeasedArtifactPath("imported-working-copy", ".png"))
            .Returns(copiedPath);
        OpenFileUseCase useCase = new(
            filePickerService.Object,
            TestNavigationCoordinator.Create(navigationService.Object),
            scratchArtifactStore.Object,
            TestFileSystem.Instance,
            recentCaptureCatalog.Object,
            TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new OpenFileRequest(), TestContext.CancellationToken);

        Assert.AreEqual(oldLastWriteTimeUtc, File.GetLastWriteTimeUtc(sourcePath));
        navigationService.Verify(
            service => service.NavigateAsync(
                NavigationRoute.ImageEdit,
                It.Is<OpenImageEditPageRequest>(request =>
                    request.ImageFile.FilePath == copiedPath &&
                    request.ImageFile.PersistentFilePath == sourcePath &&
                    request.EditorContext != null &&
                    request.EditorContext.PersistentSourcePath == sourcePath),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
        scratchArtifactStore.Verify(service => service.CreateLeasedArtifactPath("imported-working-copy", ".png"), Times.Once);
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
