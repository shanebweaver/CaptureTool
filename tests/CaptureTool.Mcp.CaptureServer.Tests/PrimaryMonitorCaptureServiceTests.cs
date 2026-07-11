using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Runtime.Versioning;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class PrimaryMonitorCaptureServiceTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 7, 11, 18, 42, 31, TimeSpan.Zero);

    [TestMethod]
    public void Capture_SelectsPrimaryMonitorAndReturnsPngWithMetadata()
    {
        MonitorCaptureResult secondary = CreateMonitor(isPrimary: false, x: -1920, y: 0, width: 1920, height: 1080, dpi: 96);
        MonitorCaptureResult primary = CreateMonitor(isPrimary: true, x: 0, y: 0, width: 2560, height: 1440, dpi: 144);
        using var bitmap = new Bitmap(3, 2);
        bitmap.SetPixel(0, 0, Color.Red);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([secondary, primary]);
        screenCapture.Setup(service => service.CreateBitmapFromMonitorCaptureResult(primary)).Returns(bitmap);
        var captureService = new PrimaryMonitorCaptureService(screenCapture.Object, new ManualTimeProvider(CaptureTime));

        PrimaryMonitorCapture capture = captureService.Capture();

        capture.PngBytes.Should().StartWith([0x89, 0x50, 0x4E, 0x47]);
        capture.Metadata.CapturedAtUtc.Should().Be(CaptureTime);
        capture.Metadata.Width.Should().Be(3);
        capture.Metadata.Height.Should().Be(2);
        capture.Metadata.Dpi.Should().Be(144);
        capture.Metadata.Scale.Should().Be(1.5f);
        capture.Metadata.MonitorBounds.Should().Be(new RectangleDto(0, 0, 2560, 1440));
        capture.Metadata.WorkAreaBounds.Should().Be(new RectangleDto(0, 0, 2560, 1392));
        capture.Metadata.IsPrimary.Should().BeTrue();
        capture.Metadata.Format.Should().Be("png");
        screenCapture.Verify(service => service.CreateBitmapFromMonitorCaptureResult(primary), Times.Once);
        screenCapture.Verify(service => service.CreateBitmapFromMonitorCaptureResult(secondary), Times.Never);
    }

    [TestMethod]
    public void Capture_WhenNoPrimaryMonitorExists_ThrowsInvalidOperationException()
    {
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture
            .Setup(service => service.CaptureAllMonitors())
            .Returns([CreateMonitor(isPrimary: false, x: 0, y: 0, width: 1920, height: 1080, dpi: 96)]);
        var captureService = new PrimaryMonitorCaptureService(screenCapture.Object, new ManualTimeProvider(CaptureTime));

        var act = () => captureService.Capture();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("No primary monitor is available to capture.");
    }

    private static MonitorCaptureResult CreateMonitor(bool isPrimary, int x, int y, int width, int height, uint dpi)
    {
        var bounds = new Rectangle(x, y, width, height);
        var workAreaBounds = new Rectangle(x, y, width, height - 48);

        return new MonitorCaptureResult(
            hMonitor: isPrimary ? 1 : 2,
            pixelBuffer: [],
            dpi,
            bounds,
            workAreaBounds,
            isPrimary);
    }
}
