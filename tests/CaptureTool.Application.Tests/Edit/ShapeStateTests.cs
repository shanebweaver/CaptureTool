using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class ShapeStateTests
{
    [TestMethod]
    public void Constructor_ShouldCaptureRectangleAndEllipseProperties()
    {
        var rectangle = new RectangleDrawable(new Vector2(1, 2), new Size(3, 4), Color.Red, Color.Blue, 5);
        var ellipse = new EllipseDrawable(new Vector2(6, 7), new Size(8, 9), Color.Green, Color.Yellow, 10);

        var rectangleState = new ShapeState(rectangle);
        var ellipseState = new ShapeState(ellipse);

        AssertShapeState(rectangleState, rectangle.Offset, rectangle.Size, rectangle.StrokeColor, rectangle.FillColor, rectangle.StrokeWidth);
        AssertShapeState(ellipseState, ellipse.Offset, ellipse.Size, ellipse.StrokeColor, ellipse.FillColor, ellipse.StrokeWidth);
    }

    [TestMethod]
    public void Constructor_ShouldCaptureLineAndArrowProperties()
    {
        var line = new LineDrawable(new Vector2(1, 2), new Vector2(3, 4), Color.Red, 5);
        var arrow = new ArrowDrawable(new Vector2(6, 7), new Vector2(8, 9), Color.Green, 10);

        var lineState = new ShapeState(line);
        var arrowState = new ShapeState(arrow);

        AssertLineState(lineState, line.Offset, line.EndPoint, line.StrokeColor, line.StrokeWidth);
        AssertLineState(arrowState, arrow.Offset, arrow.EndPoint, arrow.StrokeColor, arrow.StrokeWidth);
    }

    [TestMethod]
    public void Constructor_ShouldCaptureTextProperties()
    {
        var text = new TextDrawable(
            new Vector2(1, 2),
            new Size(3, 4),
            "caption",
            Color.Red,
            Color.Blue,
            "Segoe UI Variable",
            18);

        var state = new ShapeState(text);

        Assert.AreEqual(text.Offset, state.Offset);
        Assert.AreEqual(text.Size, state.Size);
        Assert.AreEqual(text.Text, state.Text);
        Assert.AreEqual(text.Color, state.TextColor);
        Assert.AreEqual(text.BackgroundColor, state.TextBackgroundColor);
        Assert.AreEqual(text.FontFamily, state.FontFamily);
        Assert.AreEqual(text.FontSize, state.FontSize);
    }

    [TestMethod]
    public void Constructor_ShouldUseDefaults_ForUnknownDrawable()
    {
        var state = new ShapeState(new UnknownDrawable { Offset = new Vector2(4, 5) });

        Assert.AreEqual(Vector2.Zero, state.Offset);
        Assert.AreEqual(Size.Empty, state.Size);
        Assert.AreEqual(Vector2.Zero, state.EndPoint);
        Assert.AreEqual(string.Empty, state.Text);
        Assert.AreEqual(string.Empty, state.FontFamily);
    }

    private static void AssertShapeState(
        ShapeState state,
        Vector2 offset,
        Size size,
        Color strokeColor,
        Color fillColor,
        int strokeWidth)
    {
        Assert.AreEqual(offset, state.Offset);
        Assert.AreEqual(size, state.Size);
        Assert.AreEqual(strokeColor, state.StrokeColor);
        Assert.AreEqual(fillColor, state.FillColor);
        Assert.AreEqual(strokeWidth, state.StrokeWidth);
    }

    private static void AssertLineState(
        ShapeState state,
        Vector2 offset,
        Vector2 endPoint,
        Color strokeColor,
        int strokeWidth)
    {
        Assert.AreEqual(offset, state.Offset);
        Assert.AreEqual(endPoint, state.EndPoint);
        Assert.AreEqual(strokeColor, state.StrokeColor);
        Assert.AreEqual(strokeWidth, state.StrokeWidth);
    }

    private sealed class UnknownDrawable : IDrawable
    {
        public Vector2 Offset { get; set; }
    }
}
