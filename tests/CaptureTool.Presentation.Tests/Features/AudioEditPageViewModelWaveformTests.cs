using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Audio.CopyAudioFile;
using CaptureTool.Application.Abstractions.Edit.Audio.SaveAudioFile;
using CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;
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

    private static AudioEditPageViewModel CreateViewModel(
        IOpenExternalEditorUseCase? openExternalEditorAction = null,
        IOpenAudioFolderUseCase? openAudioFolderAction = null)
    {
        return new AudioEditPageViewModel(
            Mock.Of<ISaveAudioFileUseCase>(),
            Mock.Of<ICopyAudioFileUseCase>(),
            openExternalEditorAction ?? Mock.Of<IOpenExternalEditorUseCase>(),
            openAudioFolderAction ?? Mock.Of<IOpenAudioFolderUseCase>(),
            Mock.Of<IAudioWaveformHistory>());
    }
}
