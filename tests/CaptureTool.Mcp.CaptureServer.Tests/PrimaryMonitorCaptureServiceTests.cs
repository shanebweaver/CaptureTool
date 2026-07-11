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
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([secondary, primary]);
        var expectedCapture = new McpCapture(
            [0x89, 0x50, 0x4E, 0x47],
            McpCaptureMetadata.Create(
                "capture:primary",
                CaptureTime,
                2560,
                1440,
                primary.Dpi,
                primary.Scale,
                primary.MonitorBounds,
                "primaryMonitor",
                "png",
                primary.MonitorBounds,
                primary.WorkAreaBounds,
                primary.IsPrimary));
        var imageCaptureAdapter = new Mock<ICaptureKitImageCaptureAdapter>();
        imageCaptureAdapter
            .Setup(adapter => adapter.Capture(
                It.Is<CaptureKit.Abstractions.CaptureTarget>(target =>
                    target.Kind == CaptureKit.Abstractions.CaptureTargetKind.Monitor
                    && target.MonitorHandle == primary.HMonitor),
                primary.MonitorBounds,
                "primaryMonitor",
                primary.Dpi,
                primary.Scale,
                $"hmonitor:{primary.HMonitor}",
                "Primary monitor",
                primary.MonitorBounds,
                primary.WorkAreaBounds,
                primary.IsPrimary))
            .Returns(expectedCapture);
        var captureService = new PrimaryMonitorCaptureService(screenCapture.Object, imageCaptureAdapter.Object);

        McpCapture capture = captureService.Capture();

        capture.Should().BeSameAs(expectedCapture);
        imageCaptureAdapter.VerifyAll();
    }

    [TestMethod]
    public void Capture_WhenNoPrimaryMonitorExists_ThrowsInvalidOperationException()
    {
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture
            .Setup(service => service.CaptureAllMonitors())
            .Returns([CreateMonitor(isPrimary: false, x: 0, y: 0, width: 1920, height: 1080, dpi: 96)]);
        var captureService = new PrimaryMonitorCaptureService(screenCapture.Object, Mock.Of<ICaptureKitImageCaptureAdapter>());

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
