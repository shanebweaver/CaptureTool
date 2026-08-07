using CaptureTool.Presentation.Features.CaptureOverlay;
using System.Drawing;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureOverlayWindowGeometryTests
{
    private const int TopInsetPixels = 14;
    private const double ShadowPaddingDips = 16d;

    [TestMethod]
    public void TryCalculate_At100Percent_CentersToolbarAndInflatesShadow()
    {
        Rectangle monitorBounds = new(100, 200, 1920, 1080);

        bool calculated = CaptureOverlayWindowGeometry.TryCalculate(
            monitorBounds,
            400,
            60,
            1d,
            TopInsetPixels,
            ShadowPaddingDips,
            out CaptureOverlayWindowLayout layout);

        Assert.IsTrue(calculated);
        Assert.AreEqual(new Rectangle(860, 214, 400, 60), layout.ToolbarBounds);
        Assert.AreEqual(new Rectangle(844, 198, 432, 92), layout.ShadowBounds);
        Assert.AreEqual(new Point(16, 16), layout.ShadowCasterOffset);
    }

    [TestMethod]
    public void TryCalculate_At125Percent_CeilsFractionalContentAndPadding()
    {
        Rectangle monitorBounds = new(100, 200, 1501, 900);

        bool calculated = CaptureOverlayWindowGeometry.TryCalculate(
            monitorBounds,
            333.2,
            51.3,
            1.25d,
            TopInsetPixels,
            ShadowPaddingDips,
            out CaptureOverlayWindowLayout layout);

        Assert.IsTrue(calculated);
        Assert.AreEqual(new Rectangle(642, 214, 417, 65), layout.ToolbarBounds);
        Assert.AreEqual(new Rectangle(622, 194, 457, 105), layout.ShadowBounds);
        Assert.AreEqual(new Point(20, 20), layout.ShadowCasterOffset);
    }

    [TestMethod]
    public void TryCalculate_At150Percent_SupportsNegativeMonitorOrigin()
    {
        Rectangle monitorBounds = new(-1920, -200, 1920, 1080);

        bool calculated = CaptureOverlayWindowGeometry.TryCalculate(
            monitorBounds,
            401.25,
            59.5,
            1.5d,
            TopInsetPixels,
            ShadowPaddingDips,
            out CaptureOverlayWindowLayout layout);

        Assert.IsTrue(calculated);
        Assert.AreEqual(new Rectangle(-1261, -186, 602, 90), layout.ToolbarBounds);
        Assert.AreEqual(new Rectangle(-1285, -210, 650, 138), layout.ShadowBounds);
        Assert.AreEqual(new Point(24, 24), layout.ShadowCasterOffset);
    }

    [TestMethod]
    public void TryCalculate_At200Percent_ScalesContentAndShadowButNotPhysicalTopInset()
    {
        Rectangle monitorBounds = new(1920, 0, 3840, 2160);

        bool calculated = CaptureOverlayWindowGeometry.TryCalculate(
            monitorBounds,
            468,
            76,
            2d,
            TopInsetPixels,
            ShadowPaddingDips,
            out CaptureOverlayWindowLayout layout);

        Assert.IsTrue(calculated);
        Assert.AreEqual(new Rectangle(3372, 14, 936, 152), layout.ToolbarBounds);
        Assert.AreEqual(new Rectangle(3340, -18, 1000, 216), layout.ShadowBounds);
        Assert.AreEqual(new Point(32, 32), layout.ShadowCasterOffset);
    }

    [TestMethod]
    public void TryCalculate_WhenContentWidthChanges_PreservesMonitorCenterAndTopInset()
    {
        Rectangle monitorBounds = new(0, 0, 1920, 1080);

        bool initialCalculated = CaptureOverlayWindowGeometry.TryCalculate(
            monitorBounds,
            360,
            60,
            1.25d,
            TopInsetPixels,
            ShadowPaddingDips,
            out CaptureOverlayWindowLayout initialLayout);
        bool expandedCalculated = CaptureOverlayWindowGeometry.TryCalculate(
            monitorBounds,
            416,
            60,
            1.25d,
            TopInsetPixels,
            ShadowPaddingDips,
            out CaptureOverlayWindowLayout expandedLayout);

        Assert.IsTrue(initialCalculated);
        Assert.IsTrue(expandedCalculated);
        Assert.AreEqual(new Rectangle(735, 14, 450, 75), initialLayout.ToolbarBounds);
        Assert.AreEqual(new Rectangle(700, 14, 520, 75), expandedLayout.ToolbarBounds);
        Assert.AreEqual(960d, GetHorizontalCenter(initialLayout.ToolbarBounds));
        Assert.AreEqual(960d, GetHorizontalCenter(expandedLayout.ToolbarBounds));
        Assert.AreEqual(initialLayout.ToolbarBounds.Top, expandedLayout.ToolbarBounds.Top);
        Assert.AreEqual(initialLayout.ShadowCasterOffset, expandedLayout.ShadowCasterOffset);
    }

    [TestMethod]
    public void TryCalculate_WhenCenterOffsetIsHalfPixel_FloorsToolbarPosition()
    {
        Rectangle monitorBounds = new(0, 0, 1920, 1080);

        bool calculated = CaptureOverlayWindowGeometry.TryCalculate(
            monitorBounds,
            417,
            60,
            1d,
            TopInsetPixels,
            ShadowPaddingDips,
            out CaptureOverlayWindowLayout layout);

        Assert.IsTrue(calculated);
        Assert.AreEqual(new Rectangle(751, 14, 417, 60), layout.ToolbarBounds);
        Assert.AreEqual(959.5d, GetHorizontalCenter(layout.ToolbarBounds));
    }

    [TestMethod]
    public void TryCalculate_WhenScaledShadowPaddingIsFractional_CeilsPaddingOutward()
    {
        Rectangle monitorBounds = new(0, 0, 1920, 1080);

        bool calculated = CaptureOverlayWindowGeometry.TryCalculate(
            monitorBounds,
            400,
            60,
            1.25d,
            TopInsetPixels,
            16.1d,
            out CaptureOverlayWindowLayout layout);

        Assert.IsTrue(calculated);
        Assert.AreEqual(new Rectangle(710, 14, 500, 75), layout.ToolbarBounds);
        Assert.AreEqual(new Rectangle(689, -7, 542, 117), layout.ShadowBounds);
        Assert.AreEqual(new Point(21, 21), layout.ShadowCasterOffset);
    }

    [TestMethod]
    [DataRow(0d, 60d)]
    [DataRow(-1d, 60d)]
    [DataRow(double.NaN, 60d)]
    [DataRow(double.PositiveInfinity, 60d)]
    [DataRow(400d, 0d)]
    [DataRow(400d, -1d)]
    [DataRow(400d, double.NaN)]
    [DataRow(400d, double.PositiveInfinity)]
    public void TryCalculate_WithInvalidContentSize_ReturnsFalse(double width, double height)
    {
        AssertCalculationRejected(
            new Rectangle(0, 0, 1920, 1080),
            width,
            height,
            1d,
            TopInsetPixels,
            ShadowPaddingDips);
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void TryCalculate_WithInvalidRasterizationScale_ReturnsFalse(double rasterizationScale)
    {
        AssertCalculationRejected(
            new Rectangle(0, 0, 1920, 1080),
            400,
            60,
            rasterizationScale,
            TopInsetPixels,
            ShadowPaddingDips);
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void TryCalculate_WithInvalidShadowPadding_ReturnsFalse(double shadowPaddingDips)
    {
        AssertCalculationRejected(
            new Rectangle(0, 0, 1920, 1080),
            400,
            60,
            1d,
            TopInsetPixels,
            shadowPaddingDips);
    }

    [TestMethod]
    [DataRow(0, 1080)]
    [DataRow(-1, 1080)]
    [DataRow(1920, 0)]
    [DataRow(1920, -1)]
    public void TryCalculate_WithInvalidMonitorSize_ReturnsFalse(int monitorWidth, int monitorHeight)
    {
        AssertCalculationRejected(
            new Rectangle(0, 0, monitorWidth, monitorHeight),
            400,
            60,
            1d,
            TopInsetPixels,
            ShadowPaddingDips);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void TryCalculate_WithInvalidTopInset_ReturnsFalse(int topInsetPixels)
    {
        AssertCalculationRejected(
            new Rectangle(0, 0, 1920, 1080),
            400,
            60,
            1d,
            topInsetPixels,
            ShadowPaddingDips);
    }

    [TestMethod]
    public void TryCalculate_WhenScaledDimensionsOverflow_ReturnsFalse()
    {
        AssertCalculationRejected(
            new Rectangle(0, 0, 1920, 1080),
            double.MaxValue,
            60,
            2d,
            TopInsetPixels,
            ShadowPaddingDips);
    }

    private static void AssertCalculationRejected(
        Rectangle monitorBounds,
        double logicalContentWidth,
        double logicalContentHeight,
        double rasterizationScale,
        int topInsetPixels,
        double shadowPaddingDips)
    {
        bool calculated = CaptureOverlayWindowGeometry.TryCalculate(
            monitorBounds,
            logicalContentWidth,
            logicalContentHeight,
            rasterizationScale,
            topInsetPixels,
            shadowPaddingDips,
            out CaptureOverlayWindowLayout layout);

        Assert.IsFalse(calculated);
        Assert.AreEqual(default, layout);
    }

    private static double GetHorizontalCenter(Rectangle bounds)
        => bounds.Left + (bounds.Width / 2d);
}
