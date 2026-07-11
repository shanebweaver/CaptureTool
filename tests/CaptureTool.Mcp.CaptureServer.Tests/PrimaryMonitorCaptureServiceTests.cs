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
        using var primaryBitmap = new Bitmap(2560, 1440);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([secondary, primary]);
        screenCapture.Setup(service => service.CreateBitmapFromMonitorCaptureResult(primary)).Returns(primaryBitmap);
        var captureStore = new InMemoryMcpCaptureStore();
        var captureService = new PrimaryMonitorCaptureService(screenCapture.Object, captureStore, new ManualTimeProvider(CaptureTime));

        McpCapture capture = captureService.Capture();

        capture.PngBytes.Should().StartWith([0x89, 0x50, 0x4E, 0x47]);
        capture.Metadata.CapturedAtUtc.Should().Be(CaptureTime);
        capture.Metadata.Width.Should().Be(2560);
        capture.Metadata.Height.Should().Be(1440);
        capture.Metadata.SourceKind.Should().Be("primaryMonitor");
        capture.Metadata.TargetId.Should().Be($"hmonitor:{primary.HMonitor}");
        capture.Metadata.MonitorBounds.Should().Be(RectangleDto.FromRectangle(primary.MonitorBounds));
        captureStore.TryGet(capture.Metadata.CaptureId, out McpCapture storedCapture).Should().BeTrue();
        storedCapture.Should().BeSameAs(capture);
    }

    [TestMethod]
    public void Capture_WhenNoPrimaryMonitorExists_ThrowsInvalidOperationException()
    {
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture
            .Setup(service => service.CaptureAllMonitors())
            .Returns([CreateMonitor(isPrimary: false, x: 0, y: 0, width: 1920, height: 1080, dpi: 96)]);
        var captureService = new PrimaryMonitorCaptureService(
            screenCapture.Object,
            new InMemoryMcpCaptureStore(),
            new ManualTimeProvider(CaptureTime));

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
