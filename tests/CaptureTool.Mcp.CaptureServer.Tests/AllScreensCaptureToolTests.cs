using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Drawing;
using System.Text.Json;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
public sealed class AllScreensCaptureToolTests
{
    [TestMethod]
    public void CaptureAllScreens_WithMixedDpi_ReturnsNonUniformStructuredMetadata()
    {
        MonitorSegmentDto[] segments =
        [
            new("hmonitor:1", new RectangleDto(0, 0, 100, 80), new RectangleDto(0, 0, 100, 80), 96, 1, true),
            new("hmonitor:2", new RectangleDto(100, 0, 100, 80), new RectangleDto(100, 0, 100, 80), 144, 1.5f, false),
        ];
        McpCaptureMetadata metadata = McpCaptureMetadata.Create(
            "capture:mixed",
            new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero),
            200,
            80,
            dpi: null,
            scale: null,
            new Rectangle(0, 0, 200, 80),
            "allScreens",
            "png",
            monitorSegments: segments);
        var captureService = new Mock<IAllScreensCaptureService>();
        captureService
            .Setup(service => service.Capture())
            .Returns(new McpCapture([0x89, 0x50, 0x4E, 0x47], metadata));

        var result = AllScreensCaptureTool.CaptureAllScreens(
            captureService.Object,
            Mock.Of<ILogger<AllScreensCaptureTool>>(),
            "mixed DPI test");

        result.IsError.Should().BeFalse();
        JsonElement structuredContent = result.StructuredContent!.Value;
        structuredContent.GetProperty("dpi").ValueKind.Should().Be(JsonValueKind.Null);
        structuredContent.GetProperty("scale").ValueKind.Should().Be(JsonValueKind.Null);
        structuredContent.GetProperty("isDpiScaleUniform").GetBoolean().Should().BeFalse();
        JsonElement monitorSegments = structuredContent.GetProperty("monitorSegments");
        monitorSegments.GetArrayLength().Should().Be(2);
        monitorSegments[1].GetProperty("imageBounds").GetProperty("x").GetInt32().Should().Be(100);
        monitorSegments[1].GetProperty("dpi").GetUInt32().Should().Be(144);
    }
}
