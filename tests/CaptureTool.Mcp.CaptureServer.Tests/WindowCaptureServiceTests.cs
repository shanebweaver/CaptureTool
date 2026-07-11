using CaptureKit.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;
using FluentAssertions;
using Moq;
using System.Drawing;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
public sealed class WindowCaptureServiceTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 7, 11, 18, 42, 31, TimeSpan.Zero);

    [TestMethod]
    public void ListWindows_ReturnsCandidateWindowsWithIds()
    {
        var displayCaptureService = new Mock<IDisplayCaptureService>();
        displayCaptureService
            .Setup(service => service.GetWindows())
            .Returns([
                new CaptureWindow(123, "CaptureTool", new Rectangle(10, 20, 300, 200)),
                new CaptureWindow(456, "", new Rectangle(0, 0, 100, 100)),
                new CaptureWindow(789, "Zero", Rectangle.Empty),
            ]);
        var service = CreateService(displayCaptureService.Object);

        IReadOnlyList<WindowInfoDto> windows = service.ListWindows();

        windows.Should().ContainSingle();
        windows[0].WindowId.Should().Be("hwnd:123");
        windows[0].Title.Should().Be("CaptureTool");
        windows[0].Bounds.Should().Be(new RectangleDto(10, 20, 300, 200));
    }

    [TestMethod]
    public void ListWindows_ExcludesWindowsOutsideCapturedDesktop()
    {
        var displayCaptureService = new Mock<IDisplayCaptureService>();
        displayCaptureService
            .Setup(service => service.GetWindows())
            .Returns([
                new CaptureWindow(123, "Visible", new Rectangle(10, 20, 300, 200)),
                new CaptureWindow(456, "Minimized", new Rectangle(-32000, -32000, 300, 200)),
            ]);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture
            .Setup(service => service.CaptureAllMonitors())
            .Returns([new MonitorCaptureResult(1, [], 96, new Rectangle(0, 0, 1000, 800), new Rectangle(0, 0, 1000, 760), true)]);
        var service = CreateService(displayCaptureService.Object, screenCapture.Object);

        IReadOnlyList<WindowInfoDto> windows = service.ListWindows();

        windows.Should().ContainSingle();
        windows[0].WindowId.Should().Be("hwnd:123");
    }

    [TestMethod]
    public void CaptureWindow_UsesCaptureKitWindowTarget()
    {
        var window = new CaptureWindow(123, "CaptureTool", new Rectangle(10, 20, 300, 200));
        var monitor = new MonitorCaptureResult(1, [], 96, new Rectangle(0, 0, 1000, 800), new Rectangle(0, 0, 1000, 760), true);
        var displayCaptureService = new Mock<IDisplayCaptureService>();
        displayCaptureService.Setup(service => service.GetWindows()).Returns([window]);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([monitor]);
        var expectedCapture = new McpCapture(
            [0x89, 0x50, 0x4E, 0x47],
            McpCaptureMetadata.Create("capture:window", CaptureTime, 300, 200, 96, 1, window.Bounds, "window", "png"));
        var imageCaptureAdapter = new Mock<ICaptureKitImageCaptureAdapter>();
        imageCaptureAdapter
            .Setup(adapter => adapter.Capture(
                It.Is<CaptureTarget>(target => target.Kind == CaptureTargetKind.Window && target.WindowHandle == window.Handle),
                window.Bounds,
                "window",
                monitor.Dpi,
                monitor.Scale,
                "hwnd:123",
                "CaptureTool",
                monitor.MonitorBounds,
                monitor.WorkAreaBounds,
                monitor.IsPrimary))
            .Returns(expectedCapture);
        var service = CreateService(displayCaptureService.Object, screenCapture.Object, imageCaptureAdapter.Object);

        McpCapture capture = service.CaptureWindow("hwnd:123");

        capture.Should().BeSameAs(expectedCapture);
        imageCaptureAdapter.VerifyAll();
    }

    [TestMethod]
    public void CaptureWindow_WhenCaptureKitWindowCaptureFails_PropagatesFailure()
    {
        var window = new CaptureWindow(123, "CaptureTool", new Rectangle(10, 20, 300, 200));
        var monitor = new MonitorCaptureResult(1, [], 96, new Rectangle(0, 0, 1000, 800), new Rectangle(0, 0, 1000, 760), true);
        var displayCaptureService = new Mock<IDisplayCaptureService>();
        displayCaptureService.Setup(service => service.GetWindows()).Returns([window]);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([monitor]);
        var imageCaptureAdapter = new Mock<ICaptureKitImageCaptureAdapter>();
        imageCaptureAdapter
            .Setup(adapter => adapter.Capture(
                It.Is<CaptureTarget>(target => target.Kind == CaptureTargetKind.Window && target.WindowHandle == window.Handle),
                window.Bounds,
                "window",
                monitor.Dpi,
                monitor.Scale,
                "hwnd:123",
                "CaptureTool",
                monitor.MonitorBounds,
                monitor.WorkAreaBounds,
                monitor.IsPrimary))
            .Throws(new InvalidOperationException("CaptureKit window capture failed."));
        var service = CreateService(displayCaptureService.Object, screenCapture.Object, imageCaptureAdapter.Object);

        Action act = () => service.CaptureWindow("hwnd:123");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("CaptureKit window capture failed.");
        imageCaptureAdapter.VerifyAll();
    }

    [TestMethod]
    public void CaptureWindow_PreservesPhysicalWindowBoundsOnScaledMonitor()
    {
        var window = new CaptureWindow(123, "CaptureTool", new Rectangle(0, 0, 2560, 1380));
        var monitor = new MonitorCaptureResult(1, [], 120, new Rectangle(0, 0, 2560, 1440), new Rectangle(0, 0, 2560, 1380), true);
        var displayCaptureService = new Mock<IDisplayCaptureService>();
        displayCaptureService.Setup(service => service.GetWindows()).Returns([window]);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([monitor]);
        var expectedCapture = new McpCapture(
            [0x89, 0x50, 0x4E, 0x47],
            McpCaptureMetadata.Create("capture:window", CaptureTime, 2560, 1380, monitor.Dpi, monitor.Scale, window.Bounds, "window", "png"));
        var imageCaptureAdapter = new Mock<ICaptureKitImageCaptureAdapter>();
        imageCaptureAdapter
            .Setup(adapter => adapter.Capture(
                It.Is<CaptureTarget>(target => target.Kind == CaptureTargetKind.Window && target.WindowHandle == window.Handle),
                window.Bounds,
                "window",
                monitor.Dpi,
                monitor.Scale,
                "hwnd:123",
                "CaptureTool",
                monitor.MonitorBounds,
                monitor.WorkAreaBounds,
                monitor.IsPrimary))
            .Returns(expectedCapture);
        var service = CreateService(displayCaptureService.Object, screenCapture.Object, imageCaptureAdapter.Object);

        McpCapture capture = service.CaptureWindow("hwnd:123");

        capture.Should().BeSameAs(expectedCapture);
        imageCaptureAdapter.VerifyAll();
    }

    private static WindowCaptureService CreateService(
        IDisplayCaptureService displayCaptureService,
        IScreenCapture? screenCapture = null,
        ICaptureKitImageCaptureAdapter? imageCaptureAdapter = null)
        => new(
            displayCaptureService,
            screenCapture ?? Mock.Of<IScreenCapture>(),
            imageCaptureAdapter ?? Mock.Of<ICaptureKitImageCaptureAdapter>());
}
