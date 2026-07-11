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
        captureStore.TryGet(capture.Metadata.CaptureId, out McpCapture storedCapture).Should().BeTrue();
        storedCapture.Should().BeSameAs(capture);
    }

    private static MonitorCaptureResult CreateMonitor(Rectangle bounds, bool isPrimary)
        => new(isPrimary ? 1 : 2, [], 96, bounds, bounds, isPrimary);
}
