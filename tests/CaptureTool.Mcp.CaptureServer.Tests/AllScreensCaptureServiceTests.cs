using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Runtime.Versioning;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class AllScreensCaptureServiceTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 7, 11, 18, 42, 31, TimeSpan.Zero);

    [TestMethod]
    public void Capture_CombinesMonitorsAndStoresCapture()
    {
        MonitorCaptureResult primary = CreateMonitor(new Rectangle(0, 0, 100, 80), isPrimary: true);
        MonitorCaptureResult secondary = CreateMonitor(new Rectangle(100, 0, 50, 80), isPrimary: false);
        using var combined = new Bitmap(150, 80);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([primary, secondary]);
        screenCapture.Setup(service => service.CombineMonitors(It.IsAny<IList<MonitorCaptureResult>>())).Returns(combined);
        var captureStore = new InMemoryMcpCaptureStore();
        var captureService = new AllScreensCaptureService(screenCapture.Object, captureStore, new ManualTimeProvider(CaptureTime));

        McpCapture capture = captureService.Capture();

        capture.Metadata.SourceKind.Should().Be("allScreens");
        capture.Metadata.SourceBounds.Should().Be(new RectangleDto(0, 0, 150, 80));
        capture.Metadata.Width.Should().Be(150);
        capture.Metadata.Height.Should().Be(80);
        capture.Metadata.Dpi.Should().Be(96);
        capture.Metadata.Scale.Should().Be(1);
        capture.Metadata.IsDpiScaleUniform.Should().BeTrue();
        capture.Metadata.MonitorSegments.Should().BeEquivalentTo([
            new MonitorSegmentDto("hmonitor:1", new RectangleDto(0, 0, 100, 80), new RectangleDto(0, 0, 100, 80), 96, 1, true),
            new MonitorSegmentDto("hmonitor:2", new RectangleDto(100, 0, 50, 80), new RectangleDto(100, 0, 50, 80), 96, 1, false),
        ], options => options.WithStrictOrdering());
        captureStore.TryGet(capture.Metadata.CaptureId, out McpCapture storedCapture).Should().BeTrue();
        storedCapture.Should().BeSameAs(capture);
    }

    [TestMethod]
    public void Capture_WithMixedDpi_OmitsScalarDpiAndMapsEachMonitor()
    {
        MonitorCaptureResult primary = CreateMonitor(new Rectangle(0, 0, 100, 80), isPrimary: true, dpi: 96);
        MonitorCaptureResult secondary = CreateMonitor(new Rectangle(-50, 0, 50, 80), isPrimary: false, dpi: 144);
        using var combined = new Bitmap(150, 80);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([primary, secondary]);
        screenCapture.Setup(service => service.CombineMonitors(It.IsAny<IList<MonitorCaptureResult>>())).Returns(combined);
        var captureService = new AllScreensCaptureService(
            screenCapture.Object,
            new InMemoryMcpCaptureStore(),
            new ManualTimeProvider(CaptureTime));

        McpCapture capture = captureService.Capture();

        capture.Metadata.Dpi.Should().BeNull();
        capture.Metadata.Scale.Should().BeNull();
        capture.Metadata.IsDpiScaleUniform.Should().BeFalse();
        capture.Metadata.MonitorSegments.Should().BeEquivalentTo([
            new MonitorSegmentDto("hmonitor:1", new RectangleDto(0, 0, 100, 80), new RectangleDto(50, 0, 100, 80), 96, 1, true),
            new MonitorSegmentDto("hmonitor:2", new RectangleDto(-50, 0, 50, 80), new RectangleDto(0, 0, 50, 80), 144, 1.5f, false),
        ], options => options.WithStrictOrdering());
    }

    private static MonitorCaptureResult CreateMonitor(Rectangle bounds, bool isPrimary, uint dpi = 96)
        => new(isPrimary ? 1 : 2, [], dpi, bounds, bounds, isPrimary);
}
