using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Features.SelectionOverlay;
using System.Drawing;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class SelectionOverlayWindowGeometryTests
{
    [TestMethod]
    public void TryProjectToMonitor_WindowInsideMonitor_TranslatesToMonitorCoordinates()
    {
        MonitorCaptureResult monitor = CreateMonitor(new Rectangle(100, 200, 800, 600));
        WindowInfo window = new(42, "inside", new Rectangle(250, 350, 200, 100));

        bool projected = SelectionOverlayWindowGeometry.TryProjectToMonitor(window, monitor, out WindowInfo result);

        Assert.IsTrue(projected);
        Assert.AreEqual(new Rectangle(150, 150, 200, 100), result.Position);
        Assert.AreEqual((nint)42, result.Handle);
        Assert.AreEqual("inside", result.Title);
    }

    [TestMethod]
    [DataRow(-20, 20, 120, 60, 0, 20, 100, 60)]
    [DataRow(20, -20, 60, 120, 20, 0, 60, 100)]
    [DataRow(750, 20, 100, 60, 750, 20, 50, 60)]
    [DataRow(20, 550, 60, 100, 20, 550, 60, 50)]
    public void TryProjectToMonitor_WindowCrossesOneEdge_ClipsToCanvas(
        int x,
        int y,
        int width,
        int height,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        MonitorCaptureResult monitor = CreateMonitor(new Rectangle(0, 0, 800, 600));
        WindowInfo window = new(1, "edge", new Rectangle(x, y, width, height));

        bool projected = SelectionOverlayWindowGeometry.TryProjectToMonitor(window, monitor, out WindowInfo result);

        Assert.IsTrue(projected);
        Assert.AreEqual(new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight), result.Position);
    }

    [TestMethod]
    public void TryProjectToMonitor_WindowCrossesEveryEdge_ClipsToEntireCanvas()
    {
        MonitorCaptureResult monitor = CreateMonitor(new Rectangle(0, 0, 800, 600));
        WindowInfo window = new(1, "large", new Rectangle(-20, -30, 850, 660));

        bool projected = SelectionOverlayWindowGeometry.TryProjectToMonitor(window, monitor, out WindowInfo result);

        Assert.IsTrue(projected);
        Assert.AreEqual(new Rectangle(0, 0, 800, 600), result.Position);
    }

    [TestMethod]
    public void TryProjectToMonitor_NegativeMonitorOrigin_TranslatesVisibleIntersection()
    {
        MonitorCaptureResult monitor = CreateMonitor(new Rectangle(-1920, -200, 1920, 1080));
        WindowInfo window = new(1, "negative", new Rectangle(-2000, -250, 200, 200));

        bool projected = SelectionOverlayWindowGeometry.TryProjectToMonitor(window, monitor, out WindowInfo result);

        Assert.IsTrue(projected);
        Assert.AreEqual(new Rectangle(0, 0, 120, 150), result.Position);
    }

    [TestMethod]
    public void TryProjectToMonitor_MixedDpi_RoundsLeadingEdgesDownAndTrailingEdgesUp()
    {
        MonitorCaptureResult monitor = CreateMonitor(new Rectangle(1000, 0, 1500, 900), dpi: 144);
        WindowInfo window = new(1, "scaled", new Rectangle(1001, 1, 3, 3));

        bool projected = SelectionOverlayWindowGeometry.TryProjectToMonitor(window, monitor, out WindowInfo result);

        Assert.IsTrue(projected);
        Assert.AreEqual(new Rectangle(0, 0, 3, 3), result.Position);
    }

    [TestMethod]
    [DataRow(-10, 100, 11, 100, 0, 66, 1, 68)]
    [DataRow(1499, 100, 11, 100, 999, 66, 1, 68)]
    [DataRow(100, -10, 100, 11, 66, 0, 68, 1)]
    [DataRow(100, 899, 100, 11, 66, 599, 68, 1)]
    public void TryProjectToMonitor_OnePhysicalPixelIntersection_RemainsRepresented(
        int x,
        int y,
        int width,
        int height,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        MonitorCaptureResult monitor = CreateMonitor(new Rectangle(0, 0, 1500, 900), dpi: 144);
        WindowInfo window = new(1, "one pixel", new Rectangle(x, y, width, height));

        bool projected = SelectionOverlayWindowGeometry.TryProjectToMonitor(window, monitor, out WindowInfo result);

        Assert.IsTrue(projected);
        Assert.AreEqual(new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight), result.Position);
    }

    [TestMethod]
    [DataRow(1500, 0)]
    [DataRow(-100, 0)]
    public void TryProjectToMonitor_WindowDoesNotOverlap_ReturnsFalse(int x, int y)
    {
        MonitorCaptureResult monitor = CreateMonitor(new Rectangle(0, 0, 1500, 900), dpi: 144);
        WindowInfo window = new(1, "outside", new Rectangle(x, y, 100, 100));

        bool projected = SelectionOverlayWindowGeometry.TryProjectToMonitor(window, monitor, out WindowInfo result);

        Assert.IsFalse(projected);
        Assert.AreEqual(default, result);
    }

    private static MonitorCaptureResult CreateMonitor(Rectangle bounds, uint dpi = 96)
        => new(1, [], dpi, bounds, bounds, true);
}
