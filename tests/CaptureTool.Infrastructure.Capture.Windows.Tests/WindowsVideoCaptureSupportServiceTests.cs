using CaptureTool.Application.Abstractions.Capture;
using FluentAssertions;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests;

[TestClass]
public sealed class WindowsVideoCaptureSupportServiceTests
{
    [TestMethod]
    public void GetSupportStatus_ReturnsOperatingSystemReason_WhenOsIsTooOld()
    {
        bool graphicsProbeCalled = false;
        var service = new WindowsVideoCaptureSupportService(
            () => false,
            () =>
            {
                graphicsProbeCalled = true;
                return true;
            });

        VideoCaptureSupportStatus result = service.GetSupportStatus();

        result.Should().Be(VideoCaptureSupportStatus.Unsupported(VideoCaptureUnsupportedReason.OperatingSystem));
        graphicsProbeCalled.Should().BeFalse();
    }

    [TestMethod]
    public void GetSupportStatus_ReturnsGraphicsCaptureReason_WhenWindowsReportsUnsupported()
    {
        var service = new WindowsVideoCaptureSupportService(() => true, () => false);

        VideoCaptureSupportStatus result = service.GetSupportStatus();

        result.Should().Be(VideoCaptureSupportStatus.Unsupported(VideoCaptureUnsupportedReason.GraphicsCapture));
    }

    [TestMethod]
    public void GetSupportStatus_ReturnsGraphicsCaptureReason_WhenProbeThrows()
    {
        var service = new WindowsVideoCaptureSupportService(
            () => true,
            () => throw new InvalidOperationException("probe failed"));

        VideoCaptureSupportStatus result = service.GetSupportStatus();

        result.Should().Be(VideoCaptureSupportStatus.Unsupported(VideoCaptureUnsupportedReason.GraphicsCapture));
    }

    [TestMethod]
    public void GetSupportStatus_ReturnsSupported_WhenAllChecksPass()
    {
        var service = new WindowsVideoCaptureSupportService(() => true, () => true);

        VideoCaptureSupportStatus result = service.GetSupportStatus();

        result.Should().Be(VideoCaptureSupportStatus.Supported);
    }
}
