using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Drawing;
using System.Text.Json;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
public sealed class PrimaryMonitorCaptureToolTests
{
    [TestMethod]
    public void CapturePrimaryMonitor_ReturnsImageContentAndStructuredMetadata()
    {
        var captureTime = new DateTimeOffset(2026, 7, 11, 18, 42, 31, TimeSpan.Zero);
        PrimaryMonitorCaptureMetadata metadata = PrimaryMonitorCaptureMetadata.Create(
            captureTime,
            width: 2,
            height: 1,
            dpi: 96,
            scale: 1,
            new Rectangle(0, 0, 2, 1),
            new Rectangle(0, 0, 2, 1),
            isPrimary: true,
            format: "png");
        var captureService = new Mock<IPrimaryMonitorCaptureService>();
        captureService
            .Setup(service => service.Capture())
            .Returns(new PrimaryMonitorCapture([0x89, 0x50, 0x4E, 0x47], metadata));
        var logger = new Mock<ILogger<PrimaryMonitorCaptureTool>>();

        var result = PrimaryMonitorCaptureTool.CapturePrimaryMonitor(captureService.Object, logger.Object, "progress check");

        result.IsError.Should().BeFalse();
        result.Content.Should().HaveCount(2);
        result.Content[0].Type.Should().Be("text");
        result.Content[1].Type.Should().Be("image");
        result.StructuredContent.Should().NotBeNull();

        JsonElement structuredContent = result.StructuredContent!.Value;
        structuredContent.GetProperty("width").GetInt32().Should().Be(2);
        structuredContent.GetProperty("height").GetInt32().Should().Be(1);
        structuredContent.GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        structuredContent.GetProperty("format").GetString().Should().Be("png");
    }

    [TestMethod]
    public void CapturePrimaryMonitor_WhenCaptureFails_ReturnsToolError()
    {
        var captureService = new Mock<IPrimaryMonitorCaptureService>();
        captureService
            .Setup(service => service.Capture())
            .Throws(new InvalidOperationException("No primary monitor is available to capture."));
        var logger = new Mock<ILogger<PrimaryMonitorCaptureTool>>();

        var result = PrimaryMonitorCaptureTool.CapturePrimaryMonitor(captureService.Object, logger.Object, "progress check");

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        result.Content[0].Type.Should().Be("text");
    }
}
