using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Features.AudioEdit.CopyAudioFile;
using CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Features.AudioEdit.SaveAudioFile;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Abstractions.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Application.Abstractions.Media;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.AudioEdit.CopyAudioFile;
using CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Features.AudioEdit.SaveAudioFile;
using CaptureTool.Application.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Domain.Capture.Files;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class MediaEditUseCaseTests
{
    [TestMethod]
    public async Task CopyAudioFileUseCase_WithExistingFile_CopiesToClipboard()
    {
        string audioPath = await CreateTempFileAsync("audio.wav", "audio");
        var clipboard = new Mock<IClipboardService>();
        var useCase = new CopyAudioFileUseCase(clipboard.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new CopyAudioFileRequest(audioPath)));
        CopyAudioFileResponse response = (await useCase.ExecuteAsync(new CopyAudioFileRequest(audioPath), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Copied);
        clipboard.Verify(service => service.CopyFileAsync(It.Is<ClipboardFile>(file => file.FilePath == audioPath)), Times.Once);
    }

    [TestMethod]
    public async Task CopyAudioFileUseCase_WithMissingFile_ReturnsNotCopied()
    {
        var clipboard = new Mock<IClipboardService>();
        var useCase = new CopyAudioFileUseCase(clipboard.Object, TestUseCaseExecutor.Instance);

        Assert.IsFalse(useCase.CanExecute(new CopyAudioFileRequest(@"C:\missing.wav")));
        CopyAudioFileResponse response = (await useCase.ExecuteAsync(new CopyAudioFileRequest(@"C:\missing.wav"), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Copied);
        clipboard.Verify(service => service.CopyFileAsync(It.IsAny<ClipboardFile>()), Times.Never);
    }

    [TestMethod]
    public async Task SaveAudioFileUseCase_WithDestination_CopiesFile()
    {
        string sourcePath = await CreateTempFileAsync("source.wav", "audio");
        string destinationPath = Path.Combine(CreateTestFolder(), "destination.wav");
        var picker = new Mock<IFilePickerService>();
        picker
            .Setup(service => service.PickSaveFileAsync(FilePickerType.Audio, UserFolder.Music))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == destinationPath));
        var useCase = new SaveAudioFileUseCase(picker.Object, TestUseCaseExecutor.Instance);

        SaveAudioFileResponse response = (await useCase.ExecuteAsync(new SaveAudioFileRequest(sourcePath), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Saved);
        Assert.AreEqual("audio", await File.ReadAllTextAsync(destinationPath, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task OpenAudioAndVideoEditPageUseCases_NavigateToEditRoutes()
    {
        string audioPath = await CreateTempFileAsync("source.wav", "audio");
        var audioFile = Mock.Of<IAudioFile>(file => file.FilePath == audioPath);
        var videoFile = Mock.Of<IVideoFile>(file => file.FilePath == @"C:\capture.mp4");
        var navigation = new Mock<INavigationService>();

        var audioUseCase = new OpenAudioEditPageUseCase(navigation.Object, TestUseCaseExecutor.Instance);
        var videoUseCase = new OpenVideoEditPageUseCase(navigation.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(audioUseCase.CanExecute(new OpenAudioEditPageRequest(audioFile)));
        await audioUseCase.ExecuteAsync(new OpenAudioEditPageRequest(audioFile), TestContext.CancellationToken);
        await videoUseCase.ExecuteAsync(new OpenVideoEditPageRequest(videoFile), TestContext.CancellationToken);

        navigation.Verify(service => service.Navigate(NavigationRoute.AudioEdit, audioFile, false), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.VideoEdit, videoFile, false), Times.Once);
    }

    [TestMethod]
    public async Task CopyVideoFileUseCase_WithoutTrim_CopiesOriginalVideoToClipboard()
    {
        string videoPath = await CreateTempFileAsync("source.mp4", "video");
        var clipboard = new Mock<IClipboardService>();
        var useCase = new CopyVideoFileUseCase(
            clipboard.Object,
            Mock.Of<IStorageService>(),
            Mock.Of<IVideoFileTrimmer>(),
            TestUseCaseExecutor.Instance);

        CopyVideoFileResponse response = (await useCase.ExecuteAsync(new CopyVideoFileRequest(videoPath), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Copied);
        clipboard.Verify(service => service.CopyFileAsync(It.Is<ClipboardFile>(file => file.FilePath == videoPath)), Times.Once);
    }

    [TestMethod]
    public async Task CopyVideoFileUseCase_WithTrim_TrimsToTemporaryFileBeforeCopying()
    {
        string videoPath = await CreateTempFileAsync("source.mp4", "video");
        string tempFolder = CreateTestFolder();
        var clipboard = new Mock<IClipboardService>();
        var storage = new Mock<IStorageService>();
        var trimmer = new Mock<IVideoFileTrimmer>();
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(tempFolder);
        storage.Setup(service => service.GetTemporaryFileName()).Returns("trim.tmp");
        var useCase = new CopyVideoFileUseCase(clipboard.Object, storage.Object, trimmer.Object, TestUseCaseExecutor.Instance);
        var request = new CopyVideoFileRequest(videoPath, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));

        CopyVideoFileResponse response = (await useCase.ExecuteAsync(request, TestContext.CancellationToken)).Value!;

        string expectedTrimmedPath = Path.Combine(tempFolder, "trim.mp4");
        Assert.IsTrue(response.Copied);
        trimmer.Verify(service => service.TrimAsync(videoPath, expectedTrimmedPath, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TestContext.CancellationToken), Times.Once);
        clipboard.Verify(service => service.CopyFileAsync(It.Is<ClipboardFile>(file => file.FilePath == expectedTrimmedPath)), Times.Once);
    }

    [TestMethod]
    public async Task SaveVideoFileUseCase_WithTrim_UsesVideoTrimmer()
    {
        string videoPath = await CreateTempFileAsync("source.mp4", "video");
        string destinationPath = Path.Combine(CreateTestFolder(), "destination.mp4");
        var picker = new Mock<IFilePickerService>();
        var trimmer = new Mock<IVideoFileTrimmer>();
        picker
            .Setup(service => service.PickSaveFileAsync(FilePickerType.Video, UserFolder.Videos))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == destinationPath));
        var useCase = new SaveVideoFileUseCase(picker.Object, trimmer.Object, TestUseCaseExecutor.Instance);
        var request = new SaveVideoFileRequest(videoPath, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4));

        SaveVideoFileResponse response = (await useCase.ExecuteAsync(request, TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Saved);
        trimmer.Verify(service => service.TrimAsync(videoPath, destinationPath, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task SaveVideoFileUseCase_WithoutTrim_CopiesFile()
    {
        string videoPath = await CreateTempFileAsync("source.mp4", "video");
        string destinationPath = Path.Combine(CreateTestFolder(), "destination.mp4");
        var picker = new Mock<IFilePickerService>();
        var trimmer = new Mock<IVideoFileTrimmer>();
        picker
            .Setup(service => service.PickSaveFileAsync(FilePickerType.Video, UserFolder.Videos))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == destinationPath));
        var useCase = new SaveVideoFileUseCase(picker.Object, trimmer.Object, TestUseCaseExecutor.Instance);

        SaveVideoFileResponse response = (await useCase.ExecuteAsync(new SaveVideoFileRequest(videoPath), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Saved);
        Assert.AreEqual("video", await File.ReadAllTextAsync(destinationPath, TestContext.CancellationToken));
        trimmer.Verify(service => service.TrimAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task<string> CreateTempFileAsync(string fileName, string contents)
    {
        string folder = CreateTestFolder();
        string path = Path.Combine(folder, fileName);
        await File.WriteAllTextAsync(path, contents);
        return path;
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    public TestContext TestContext { get; set; } = null!;
}
