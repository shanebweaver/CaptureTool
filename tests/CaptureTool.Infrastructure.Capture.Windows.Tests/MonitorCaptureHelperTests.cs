using CaptureTool.Domain.Capture;
using FluentAssertions;
using System.Drawing;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests;

[TestClass]
public sealed class MonitorCaptureHelperTests
{
    [TestMethod]
    public void CombineMonitors_WithNoMonitors_ThrowsArgumentException()
    {
        var act = () => MonitorCaptureHelper.CombineMonitors([]);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("monitors");
    }

    [TestMethod]
    public void CombineMonitors_WithAdjacentMonitors_ReturnsCombinedBitmap()
    {
        var monitors = new[]
        {
            CreateMonitor(new Rectangle(0, 0, 1, 1), red: 255, green: 0, blue: 0),
            CreateMonitor(new Rectangle(1, 0, 1, 1), red: 0, green: 0, blue: 255),
        };

        using var bitmap = MonitorCaptureHelper.CombineMonitors(monitors);

        bitmap.Width.Should().Be(2);
        bitmap.Height.Should().Be(1);
        bitmap.GetPixel(0, 0).Should().Be(Color.FromArgb(255, 255, 0, 0));
        bitmap.GetPixel(1, 0).Should().Be(Color.FromArgb(255, 0, 0, 255));
    }

    private static MonitorCaptureResult CreateMonitor(Rectangle bounds, byte red, byte green, byte blue)
    {
        var pixelBuffer = new byte[bounds.Width * bounds.Height * 4];
        for (var index = 0; index < pixelBuffer.Length; index += 4)
        {
            pixelBuffer[index] = blue;
            pixelBuffer[index + 1] = green;
            pixelBuffer[index + 2] = red;
            pixelBuffer[index + 3] = 255;
        }

        return new MonitorCaptureResult(
            hMonitor: 0,
            pixelBuffer,
            dpi: 96,
            monitorBounds: bounds,
            workAreaBounds: bounds,
            isPrimary: bounds.X == 0);
    }
}
