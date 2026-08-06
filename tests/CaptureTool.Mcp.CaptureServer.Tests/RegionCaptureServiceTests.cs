using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Runtime.Versioning;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class RegionCaptureServiceTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 7, 11, 18, 42, 31, TimeSpan.Zero);

    [TestMethod]
    public void Capture_CropsRequestedRegionAndStoresCapture()
    {
        MonitorCaptureResult monitor = CreateMonitor(new Rectangle(0, 0, 10, 10));
        using var monitorBitmap = new Bitmap(10, 10);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([monitor]);
        screenCapture.Setup(service => service.CreateBitmapFromMonitorCaptureResult(monitor)).Returns(monitorBitmap);
        var captureStore = new InMemoryMcpCaptureStore();
        var captureService = new RegionCaptureService(
            screenCapture.Object,
            captureStore,
            new ManualTimeProvider(CaptureTime));

        McpCapture capture = captureService.Capture(2, 3, 4, 4);

        capture.PngBytes.Should().StartWith([0x89, 0x50, 0x4E, 0x47]);
        capture.Metadata.Width.Should().Be(4);
        capture.Metadata.Height.Should().Be(4);
        capture.Metadata.SourceBounds.Should().Be(new RectangleDto(2, 3, 4, 4));
        capture.Metadata.MonitorBounds.Should().Be(RectangleDto.FromRectangle(monitor.MonitorBounds));
        captureStore.TryGet(capture.Metadata.CaptureId, out McpCapture storedCapture).Should().BeTrue();
        storedCapture.Should().BeSameAs(capture);
    }

    [TestMethod]
    public void Capture_WhenRegionDoesNotIntersectAnyMonitor_ThrowsInvalidOperationException()
    {
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture
            .Setup(service => service.CaptureAllMonitors())
            .Returns([CreateMonitor(new Rectangle(0, 0, 10, 10))]);
        var captureService = new RegionCaptureService(
            screenCapture.Object,
            new InMemoryMcpCaptureStore(),
            new ManualTimeProvider(CaptureTime));

        var act = () => captureService.Capture(20, 20, 4, 4);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("The requested capture region does not intersect any monitor.");
    }

    [TestMethod]
    public void Capture_WhenRegionSpansMixedDpiMonitors_MapsSegmentsWithoutScalarDpi()
    {
        MonitorCaptureResult primary = CreateMonitor(new Rectangle(0, 0, 100, 80), hMonitor: 1, dpi: 96, isPrimary: true);
        MonitorCaptureResult secondary = CreateMonitor(new Rectangle(100, 0, 100, 80), hMonitor: 2, dpi: 144);
        using var combined = new Bitmap(200, 80);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([primary, secondary]);
        screenCapture.Setup(service => service.CombineMonitors(It.IsAny<IList<MonitorCaptureResult>>())).Returns(combined);
        var captureService = new RegionCaptureService(
            screenCapture.Object,
            new InMemoryMcpCaptureStore(),
            new ManualTimeProvider(CaptureTime));

        McpCapture capture = captureService.Capture(50, 10, 100, 50);

        capture.Metadata.Dpi.Should().BeNull();
        capture.Metadata.Scale.Should().BeNull();
        capture.Metadata.IsDpiScaleUniform.Should().BeFalse();
        capture.Metadata.MonitorBounds.Should().BeNull();
        capture.Metadata.MonitorSegments.Should().BeEquivalentTo([
            new MonitorSegmentDto("hmonitor:1", new RectangleDto(50, 10, 50, 50), new RectangleDto(0, 0, 50, 50), 96, 1, true),
            new MonitorSegmentDto("hmonitor:2", new RectangleDto(100, 10, 50, 50), new RectangleDto(50, 0, 50, 50), 144, 1.5f, false),
        ], options => options.WithStrictOrdering());
    }

    private static MonitorCaptureResult CreateMonitor(
        Rectangle bounds,
        nint hMonitor = 1,
        uint dpi = 96,
        bool isPrimary = false)
        => new(hMonitor, [], dpi, bounds, bounds, isPrimary);
}
