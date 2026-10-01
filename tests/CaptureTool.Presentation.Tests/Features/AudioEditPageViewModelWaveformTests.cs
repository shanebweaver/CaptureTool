using CaptureTool.Application.Abstractions.Edit.Audio.CopyAudioFile;
using CaptureTool.Application.Abstractions.Edit.Audio.SaveAudioFile;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.Audio;
using CaptureTool.Presentation.Features.AudioEdit;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class AudioEditPageViewModelWaveformTests
{
    [TestMethod]
    public void SetWaveformLevels_ShouldCreateOneBarPerLevel()
    {
        AudioEditPageViewModel viewModel = CreateViewModel();

        viewModel.SetWaveformLevels([-1, .5, 2]);

        viewModel.WaveformBars.Should().HaveCount(3);
        viewModel.WaveformBars[0].Height.Should().Be(0);
        viewModel.WaveformBars[1].Height.Should().Be(66);
        viewModel.WaveformBars[2].Height.Should().Be(132);
        viewModel.WaveformBars[0].Level.Should().Be(0);
        viewModel.WaveformBars[1].Level.Should().Be(.5);
        viewModel.WaveformBars[2].Level.Should().Be(1);
    }

    [TestMethod]
    public void SetWaveformLevels_ShouldReplaceExistingBars()
    {
        AudioEditPageViewModel viewModel = CreateViewModel();

        viewModel.SetWaveformLevels([0, .25, .5, .75, 1]);
        viewModel.SetWaveformLevels([.5]);

        viewModel.WaveformBars.Should().ContainSingle();
        viewModel.WaveformBars[0].Height.Should().Be(66);
        viewModel.WaveformBars[0].Level.Should().Be(.5);
    }

    [TestMethod]
    public async Task OpenInClipchampCommand_ShouldOpenCurrentAudioInClipchamp()
    {
        var externalEditor = new Mock<IOpenExternalEditorUseCase>();
        AudioEditPageViewModel viewModel = CreateViewModel(openExternalEditorAction: externalEditor.Object);
        viewModel.Load(new AudioFile("test.wav"));

        await viewModel.OpenInClipchampCommand.ExecuteAsync(null);

        externalEditor.Verify(service =>
            service.ExecuteAsync(
                It.Is<OpenExternalEditorRequest>(request =>
                    request.MediaPath == "test.wav" &&
                    request.Editor == ExternalMediaEditor.Clipchamp),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task OpenAudioFolderCommand_ShouldOpenAudioFolder()
    {
        var openAudioFolder = new Mock<IOpenAudioFolderUseCase>();
        openAudioFolder
            .Setup(service => service.ExecuteAsync(It.IsAny<OpenAudioFolderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<OpenAudioFolderResponse>.Success(new OpenAudioFolderResponse()));
        AudioEditPageViewModel viewModel = CreateViewModel(openAudioFolderAction: openAudioFolder.Object);

        await viewModel.OpenAudioFolderCommand.ExecuteAsync(null);

        openAudioFolder.Verify(
            service => service.ExecuteAsync(It.IsAny<OpenAudioFolderRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void Dispose_ShouldReleaseOwnedWorkingCopy()
    {
        var scratchArtifactStore = new Mock<IScratchArtifactStore>();
        AudioEditPageViewModel viewModel = CreateViewModel(scratchArtifactStore: scratchArtifactStore.Object);
        viewModel.Load(new AudioFile("scratch\\working.wav"));

        viewModel.Dispose();

        scratchArtifactStore.Verify(
            store => store.DeleteArtifact("scratch\\working.wav"),
            Times.Once);
    }

    [TestMethod]
    public void Load_AfterPreviousEditorDisposed_ShouldRecreateWorkingCopy()
    {
        var existingFiles = new HashSet<string> { "scratch\\working.wav", "recordings\\source.wav" };
        var files = new Mock<IFileSystem>();
        files.Setup(service => service.FileExists(It.IsAny<string>()))
            .Returns<string>(existingFiles.Contains);
        var scratchArtifactStore = new Mock<IScratchArtifactStore>();
        scratchArtifactStore.Setup(store => store.DeleteArtifact(It.IsAny<string>()))
            .Callback<string>(path => existingFiles.Remove(path));
        scratchArtifactStore.Setup(store => store.CreateLeasedArtifactPath(It.IsAny<string>(), ".wav"))
            .Returns("scratch\\restored.wav");
        var navigationFile = new AudioFile("scratch\\working.wav", "recordings\\source.wav");
        var previous = CreateViewModel(scratchArtifactStore: scratchArtifactStore.Object, fileSystem: files.Object);
        previous.Load(navigationFile);
        files.Verify(service => service.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        previous.Dispose();

        var reopened = CreateViewModel(scratchArtifactStore: scratchArtifactStore.Object, fileSystem: files.Object);
        reopened.Load(navigationFile);

        reopened.AudioPath.Should().Be("scratch\\restored.wav");
        reopened.DetailsSourcePath.Should().Be("recordings\\source.wav");
        navigationFile.FilePath.Should().Be("scratch\\restored.wav");
        files.Verify(service => service.CopyFile("recordings\\source.wav", "scratch\\restored.wav", true), Times.Once);
        reopened.Dispose();
        scratchArtifactStore.Verify(store => store.DeleteArtifact("scratch\\working.wav"), Times.Once);
        scratchArtifactStore.Verify(store => store.DeleteArtifact("scratch\\restored.wav"), Times.Once);
        scratchArtifactStore.Verify(store => store.DeleteArtifact("recordings\\source.wav"), Times.Never);
    }

    [TestMethod]
    public void Load_WhenRestoringWorkingCopyFails_ShouldReleaseIncompleteCopy()
    {
        var files = new Mock<IFileSystem>();
        files.Setup(service => service.FileExists("recordings\\source.wav")).Returns(true);
        files.Setup(service => service.CopyFile("recordings\\source.wav", "scratch\\restored.wav", true))
            .Throws(new IOException("Copy failed."));
        var scratchArtifactStore = new Mock<IScratchArtifactStore>();
        scratchArtifactStore.Setup(store => store.CreateLeasedArtifactPath(It.IsAny<string>(), ".wav"))
            .Returns("scratch\\restored.wav");
        var navigationFile = new AudioFile("scratch\\working.wav", "recordings\\source.wav");
        using var viewModel = CreateViewModel(scratchArtifactStore: scratchArtifactStore.Object, fileSystem: files.Object);

        Action load = () => viewModel.Load(navigationFile);

        load.Should().Throw<IOException>().WithMessage("Copy failed.");
        navigationFile.FilePath.Should().Be("scratch\\working.wav");
        scratchArtifactStore.Verify(store => store.DeleteArtifact("scratch\\restored.wav"), Times.Once);
    }

    private static AudioEditPageViewModel CreateViewModel(
        IOpenExternalEditorUseCase? openExternalEditorAction = null,
        IOpenAudioFolderUseCase? openAudioFolderAction = null,
        IScratchArtifactStore? scratchArtifactStore = null,
        IFileSystem? fileSystem = null)
    {
        return new AudioEditPageViewModel(
            Mock.Of<ISaveAudioFileUseCase>(),
            Mock.Of<ICopyAudioFileUseCase>(),
            openExternalEditorAction ?? Mock.Of<IOpenExternalEditorUseCase>(),
            openAudioFolderAction ?? Mock.Of<IOpenAudioFolderUseCase>(),
            Mock.Of<IAudioWaveformHistory>(),
            scratchArtifactStore: scratchArtifactStore,
            fileSystem: fileSystem);
    }
}
