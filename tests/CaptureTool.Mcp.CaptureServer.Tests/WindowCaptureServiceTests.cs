using CaptureKit.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
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
    public void ListWindows_UsesTopologyWithoutCapturingPixels()
    {
        var displayCaptureService = new Mock<IDisplayCaptureService>(MockBehavior.Strict);
        displayCaptureService
            .Setup(service => service.GetWindows())
            .Returns([
                new CaptureWindow(123, "Visible", new Rectangle(10, 20, 300, 200)),
                new CaptureWindow(456, "Minimized", new Rectangle(-32000, -32000, 300, 200)),
                new CaptureWindow(789, "Secondary", new Rectangle(-500, 20, 300, 200)),
                new CaptureWindow(999, "Spanning", new Rectangle(-50, 20, 300, 200)),
            ]);
        var topology = new Mock<IDisplayTopology>(MockBehavior.Strict);
        topology.Setup(service => service.GetMonitorBounds()).Returns([
            new Rectangle(0, 0, 1000, 800),
            new Rectangle(-1000, 0, 1000, 800),
        ]);
        var screenCapture = new Mock<IScreenCapture>(MockBehavior.Strict);
        var imageCaptureAdapter = new Mock<ICaptureKitImageCaptureAdapter>(MockBehavior.Strict);
        var service = CreateService(displayCaptureService.Object, screenCapture.Object, imageCaptureAdapter.Object, topology.Object);

        IReadOnlyList<WindowInfoDto> windows = service.ListWindows();

        windows.Select(window => window.WindowId).Should().Equal("hwnd:789", "hwnd:999", "hwnd:123");
        topology.Verify(service => service.GetMonitorBounds(), Times.Once);
        displayCaptureService.Verify(service => service.GetWindows(), Times.Once);
        displayCaptureService.VerifyNoOtherCalls();
        screenCapture.VerifyNoOtherCalls();
        imageCaptureAdapter.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void ListWindows_WhenNoMonitorsAreAvailable_ReturnsNoWindows()
    {
        var displayCaptureService = new Mock<IDisplayCaptureService>();
        displayCaptureService.Setup(service => service.GetWindows())
            .Returns([new CaptureWindow(123, "Window", new Rectangle(10, 20, 300, 200))]);
        var topology = new Mock<IDisplayTopology>();
        topology.Setup(service => service.GetMonitorBounds()).Returns([]);
        var service = CreateService(displayCaptureService.Object, displayTopology: topology.Object);

        service.ListWindows().Should().BeEmpty();
    }

    [TestMethod]
    public void ListWindows_WhenTopologyFails_ReturnsRecoverableToolErrorAndCanRetry()
    {
        var displayCaptureService = new Mock<IDisplayCaptureService>(MockBehavior.Strict);
        displayCaptureService.Setup(service => service.GetWindows())
            .Returns([new CaptureWindow(123, "Window", new Rectangle(10, 20, 300, 200))]);
        var topology = new Mock<IDisplayTopology>();
        topology.SetupSequence(service => service.GetMonitorBounds())
            .Throws(new InvalidOperationException("Display topology could not be enumerated. Check the desktop session and retry."))
            .Returns([new Rectangle(0, 0, 1000, 800)]);
        var screenCapture = new Mock<IScreenCapture>(MockBehavior.Strict);
        var imageCaptureAdapter = new Mock<ICaptureKitImageCaptureAdapter>(MockBehavior.Strict);
        var service = CreateService(displayCaptureService.Object, screenCapture.Object, imageCaptureAdapter.Object, topology.Object);

        var result = WindowCaptureTool.ListWindows(service, NullLogger<WindowCaptureTool>.Instance);

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle().Which.Should().BeOfType<TextContentBlock>()
            .Which.Text.Should().Contain("Check the desktop session and retry");
        service.ListWindows().Should().ContainSingle();
        screenCapture.VerifyNoOtherCalls();
        imageCaptureAdapter.VerifyNoOtherCalls();
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
        ICaptureKitImageCaptureAdapter? imageCaptureAdapter = null,
        IDisplayTopology? displayTopology = null)
        => new(
            displayCaptureService,
            displayTopology ?? Mock.Of<IDisplayTopology>(topology => topology.GetMonitorBounds() == new[] { new Rectangle(0, 0, 1000, 800) }),
            screenCapture ?? Mock.Of<IScreenCapture>(),
            imageCaptureAdapter ?? Mock.Of<ICaptureKitImageCaptureAdapter>());
}
