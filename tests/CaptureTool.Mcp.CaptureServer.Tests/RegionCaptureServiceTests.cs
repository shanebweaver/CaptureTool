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
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([monitor]);
        var expectedCapture = new McpCapture(
            [0x89, 0x50, 0x4E, 0x47],
            McpCaptureMetadata.Create("capture:region", CaptureTime, 4, 4, 96, 1, new Rectangle(2, 3, 4, 4), "region", "png"));
        var imageCaptureAdapter = new Mock<ICaptureKitImageCaptureAdapter>();
        imageCaptureAdapter
            .Setup(adapter => adapter.Capture(
                It.Is<CaptureKit.Abstractions.CaptureTarget>(target =>
                    target.Kind == CaptureKit.Abstractions.CaptureTargetKind.Rectangle
                    && target.MonitorHandle == monitor.HMonitor
                    && target.Left == 2
                    && target.Top == 3
                    && target.Width == 4
                    && target.Height == 4),
                new Rectangle(2, 3, 4, 4),
                "region",
                monitor.Dpi,
                monitor.Scale,
                null,
                null,
                monitor.MonitorBounds,
                monitor.WorkAreaBounds,
                monitor.IsPrimary))
            .Returns(expectedCapture);
        var captureService = new RegionCaptureService(
            screenCapture.Object,
            imageCaptureAdapter.Object,
            new InMemoryMcpCaptureStore(),
            new ManualTimeProvider(CaptureTime));

        McpCapture capture = captureService.Capture(2, 3, 4, 4);

        capture.Should().BeSameAs(expectedCapture);
        imageCaptureAdapter.VerifyAll();
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
            Mock.Of<ICaptureKitImageCaptureAdapter>(),
            new InMemoryMcpCaptureStore(),
            new ManualTimeProvider(CaptureTime));

        var act = () => captureService.Capture(20, 20, 4, 4);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("The requested capture region does not intersect any monitor.");
    }

    private static MonitorCaptureResult CreateMonitor(Rectangle bounds)
        => new(1, [], 96, bounds, bounds, true);
}
