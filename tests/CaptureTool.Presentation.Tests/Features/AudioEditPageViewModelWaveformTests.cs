using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
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
    public void SetWaveformLevels_ShouldMapLevelsToBarHeightsAndPadRemainingBars()
    {
        AudioEditPageViewModel viewModel = CreateViewModel();

        viewModel.SetWaveformLevels([0, .5, 1]);

        viewModel.WaveformBars.Should().HaveCount(64);
        viewModel.WaveformBars[0].Height.Should().Be(6);
        viewModel.WaveformBars[1].Height.Should().Be(69);
        viewModel.WaveformBars[2].Height.Should().Be(132);
        viewModel.WaveformBars[3].Height.Should().Be(6);
    }

    [TestMethod]
    public void AppendWaveformLevel_ShouldRecycleFirstBarAtEnd()
    {
        AudioEditPageViewModel viewModel = CreateViewModel();
        viewModel.SetWaveformLevels(Enumerable.Range(0, 64).Select(index => index / 63d).ToArray());
        var firstBar = viewModel.WaveformBars[0];
        var secondBar = viewModel.WaveformBars[1];

        viewModel.AppendWaveformLevel(1);

        viewModel.WaveformBars.Should().HaveCount(64);
        viewModel.WaveformBars[0].Should().BeSameAs(secondBar);
        viewModel.WaveformBars[63].Should().BeSameAs(firstBar);
        viewModel.WaveformBars[63].Height.Should().Be(132);
    }

    private static AudioEditPageViewModel CreateViewModel()
    {
        return new AudioEditPageViewModel(
            Mock.Of<ISaveAudioFileUseCase>(),
            Mock.Of<ICopyAudioFileUseCase>(),
            Mock.Of<IOpenAudioCapturePageUseCase>(),
            Mock.Of<IAudioWaveformHistory>());
    }
}
