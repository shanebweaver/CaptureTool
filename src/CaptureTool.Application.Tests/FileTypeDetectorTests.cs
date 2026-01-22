using CaptureTool.Application.Implementations;
using CaptureTool.Domain.Capture.Interfaces;
using FluentAssertions;

namespace CaptureTool.Application.Tests;

[TestClass]
public class FileTypeDetectorTests
{
    private FileTypeDetector _detector = null!;

    [TestInitialize]
    public void Init()
    {
        _detector = new FileTypeDetector();
    }

    [TestMethod]
    public void DetectFileType_WithPngExtension_ReturnsImage()
    {
        var result = _detector.DetectFileType("test.png");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithJpgExtension_ReturnsImage()
    {
        var result = _detector.DetectFileType("test.jpg");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithJpegExtension_ReturnsImage()
    {
        var result = _detector.DetectFileType("test.jpeg");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithBmpExtension_ReturnsImage()
    {
        var result = _detector.DetectFileType("test.bmp");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithGifExtension_ReturnsImage()
    {
        var result = _detector.DetectFileType("test.gif");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void DetectFileType_WithMp4Extension_ReturnsVideo()
    {
        var result = _detector.DetectFileType("test.mp4");
        result.Should().Be(CaptureFileType.Video);
    }

    [TestMethod]
    public void DetectFileType_WithAviExtension_ReturnsVideo()
    {
        var result = _detector.DetectFileType("test.avi");
        result.Should().Be(CaptureFileType.Video);
    }

    [TestMethod]
    public void DetectFileType_WithMovExtension_ReturnsVideo()
    {
        var result = _detector.DetectFileType("test.mov");
        result.Should().Be(CaptureFileType.Video);
    }

    [TestMethod]
    public void DetectFileType_WithWmvExtension_ReturnsVideo()
    {
        var result = _detector.DetectFileType("test.wmv");
        result.Should().Be(CaptureFileType.Video);
    }

    [TestMethod]
    public void DetectFileType_WithUnknownExtension_ReturnsUnknown()
    {
        var result = _detector.DetectFileType("test.txt");
        result.Should().Be(CaptureFileType.Unknown);
    }

    [TestMethod]
    public void DetectFileType_WithUppercaseExtension_ReturnsCorrectType()
    {
        var result = _detector.DetectFileType("test.PNG");
        result.Should().Be(CaptureFileType.Image);
    }

    [TestMethod]
    public void IsImageFile_WithImageExtension_ReturnsTrue()
    {
        var result = _detector.IsImageFile("test.png");
        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsImageFile_WithVideoExtension_ReturnsFalse()
    {
        var result = _detector.IsImageFile("test.mp4");
        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsVideoFile_WithVideoExtension_ReturnsTrue()
    {
        var result = _detector.IsVideoFile("test.mp4");
        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsVideoFile_WithImageExtension_ReturnsFalse()
    {
        var result = _detector.IsVideoFile("test.png");
        result.Should().BeFalse();
    }
}
