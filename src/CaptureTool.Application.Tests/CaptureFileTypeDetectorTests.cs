using CaptureTool.Domain.Capture;
using FluentAssertions;

namespace CaptureTool.Application.Tests;

[TestClass]
public class CaptureFileTypeDetectorTests
{
    [TestMethod]
    public void DetectFileType_WithPngExtension_ReturnsImage()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.png");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithJpgExtension_ReturnsImage()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.jpg");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithJpegExtension_ReturnsImage()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.jpeg");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithBmpExtension_ReturnsImage()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.bmp");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithGifExtension_ReturnsImage()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.gif");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithMp4Extension_ReturnsVideo()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.mp4");
        result.Should().Be(CaptureFileType.Video);
    }

    [TestMethod]
    public void DetectFileType_WithAviExtension_ReturnsVideo()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.avi");
        result.Should().Be(CaptureFileType.Video);
    }

    [TestMethod]
    public void DetectFileType_WithMovExtension_ReturnsVideo()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.mov");
        result.Should().Be(CaptureFileType.Video);
    }

    [TestMethod]
    public void DetectFileType_WithWmvExtension_ReturnsVideo()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.wmv");
        result.Should().Be(CaptureFileType.Video);
    }

    [TestMethod]
    public void DetectFileType_WithUnknownExtension_ReturnsUnknown()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.txt");
        result.Should().Be(CaptureFileType.Unknown);
    }

    [TestMethod]
    public void DetectFileType_WithUppercaseExtension_ReturnsCorrectType()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.PNG");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void IsImageFile_WithImageExtension_ReturnsTrue()
    {
        var result = CaptureFileTypeDetector.IsImageFile("test.png");
        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsImageFile_WithVideoExtension_ReturnsFalse()
    {
        var result = CaptureFileTypeDetector.IsImageFile("test.mp4");
        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsVideoFile_WithVideoExtension_ReturnsTrue()
    {
        var result = CaptureFileTypeDetector.IsVideoFile("test.mp4");
        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsVideoFile_WithImageExtension_ReturnsFalse()
    {
        var result = CaptureFileTypeDetector.IsVideoFile("test.png");
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectFileType_WithMp3Extension_ReturnsAudio()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.mp3");
        result.Should().Be(CaptureFileType.Audio);
    }

    [TestMethod]
    public void DetectFileType_WithWavExtension_ReturnsAudio()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.wav");
        result.Should().Be(CaptureFileType.Audio);
    }

    [TestMethod]
    public void DetectFileType_WithFlacExtension_ReturnsAudio()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.flac");
        result.Should().Be(CaptureFileType.Audio);
    }

    [TestMethod]
    public void DetectFileType_WithUppercaseAudioExtension_ReturnsAudio()
    {
        var result = CaptureFileTypeDetector.DetectFileType("test.MP3");
        result.Should().Be(CaptureFileType.Audio);
    }

    [TestMethod]
    public void IsAudioFile_WithAudioExtension_ReturnsTrue()
    {
        var result = CaptureFileTypeDetector.IsAudioFile("test.mp3");
        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsAudioFile_WithVideoExtension_ReturnsFalse()
    {
        var result = CaptureFileTypeDetector.IsAudioFile("test.mp4");
        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsAudioFile_WithImageExtension_ReturnsFalse()
    {
        var result = CaptureFileTypeDetector.IsAudioFile("test.png");
        result.Should().BeFalse();
    }
}
