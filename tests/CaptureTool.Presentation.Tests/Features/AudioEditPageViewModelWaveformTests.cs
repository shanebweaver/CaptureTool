using CaptureTool.Application.Abstractions.Edit.Audio.CopyAudioFile;
using CaptureTool.Application.Abstractions.Edit.Audio.SaveAudioFile;
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

    private static AudioEditPageViewModel CreateViewModel()
    {
        return new AudioEditPageViewModel(
            Mock.Of<ISaveAudioFileUseCase>(),
            Mock.Of<ICopyAudioFileUseCase>(),
            Mock.Of<IAudioWaveformHistory>());
    }
}
