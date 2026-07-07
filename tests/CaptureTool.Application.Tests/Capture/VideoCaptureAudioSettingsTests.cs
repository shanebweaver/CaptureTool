using CaptureTool.Application.Features.VideoCapture;
using FluentAssertions;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class VideoCaptureAudioSettingsTests
{
    [TestMethod]
    public void ShouldCaptureAudio_ShouldReflectMuteDesktopAudioAndSelectedInput()
    {
        new VideoCaptureAudioSettings(false, false, 100, null)
            .ShouldCaptureAudio.Should().BeFalse();

        new VideoCaptureAudioSettings(true, false, 100, null)
            .ShouldCaptureAudio.Should().BeTrue();

        new VideoCaptureAudioSettings(false, false, 100, "microphone")
            .ShouldCaptureAudio.Should().BeTrue();

        new VideoCaptureAudioSettings(true, true, 100, "microphone")
            .ShouldCaptureAudio.Should().BeFalse();
    }

    [TestMethod]
    public void WithAudioInputVolume_ShouldClampToValidRange()
    {
        VideoCaptureAudioSettings.Default.WithAudioInputVolume(-10).AudioInputVolumePercentage.Should().Be(0);
        VideoCaptureAudioSettings.Default.WithAudioInputVolume(37).AudioInputVolumePercentage.Should().Be(37);
        VideoCaptureAudioSettings.Default.WithAudioInputVolume(200).AudioInputVolumePercentage.Should().Be(100);
    }

    [TestMethod]
    public void WithAudioInputSource_ShouldNormalizeBlankSourceToNull()
    {
        VideoCaptureAudioSettings.Default.WithAudioInputSource("   ").SelectedAudioInputSourceId.Should().BeNull();
        VideoCaptureAudioSettings.Default.WithAudioInputSource("microphone").SelectedAudioInputSourceId.Should().Be("microphone");
    }

    [TestMethod]
    public void PrepareForCapture_ShouldResetCaptureDefaultsAndKeepSelectedSource()
    {
        var settings = new VideoCaptureAudioSettings(false, true, 42, "microphone");

        VideoCaptureAudioSettings prepared = settings.PrepareForCapture(defaultDesktopAudioEnabled: true);

        prepared.IsDesktopAudioEnabled.Should().BeTrue();
        prepared.IsAudioInputMuted.Should().BeFalse();
        prepared.AudioInputVolumePercentage.Should().Be(100);
        prepared.SelectedAudioInputSourceId.Should().Be("microphone");
    }
}
