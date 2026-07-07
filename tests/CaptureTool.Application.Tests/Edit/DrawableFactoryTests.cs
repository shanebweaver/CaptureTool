using CaptureTool.Application.Tests;
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class DrawableFactoryTests
{
    [TestMethod]
    public void CreateShape_ShouldCreateRectangleFromAnyDragDirection()
    {
        var style = new ShapeStyle(Color.Red, Color.Blue, 3);

        IDrawable? drawable = DrawableFactory.CreateShape(
            ShapeType.Rectangle,
            new Vector2(12.25f, 18.75f),
            new Vector2(2.5f, 4.25f),
            style);

        Assert.IsInstanceOfType<RectangleDrawable>(drawable);
        var rectangle = (RectangleDrawable)drawable;
        Assert.AreEqual(new Vector2(2.5f, 4.25f), rectangle.Offset);
        Assert.AreEqual(new Size(10, 15), rectangle.Size);
        Assert.AreEqual(Color.Red, rectangle.StrokeColor);
        Assert.AreEqual(Color.Blue, rectangle.FillColor);
        Assert.AreEqual(3, rectangle.StrokeWidth);
    }

    [TestMethod]
    public void CreateShape_ShouldCreateEllipseFromAnyDragDirection()
    {
        var style = new ShapeStyle(Color.Black, Color.White, 5);

        IDrawable? drawable = DrawableFactory.CreateShape(
            ShapeType.Ellipse,
            new Vector2(-3.2f, 8.1f),
            new Vector2(3.8f, 1.6f),
            style);

        Assert.IsInstanceOfType<EllipseDrawable>(drawable);
        var ellipse = (EllipseDrawable)drawable;
        Assert.AreEqual(new Vector2(-3.2f, 1.6f), ellipse.Offset);
        Assert.AreEqual(new Size(7, 7), ellipse.Size);
        Assert.AreEqual(Color.Black, ellipse.StrokeColor);
        Assert.AreEqual(Color.White, ellipse.FillColor);
        Assert.AreEqual(5, ellipse.StrokeWidth);
    }

    [TestMethod]
    public void CreateShape_ShouldCreateLineAndArrowWithExactEndpoints()
    {
        var style = new ShapeStyle(Color.Green, Color.Empty, 7);
        var start = new Vector2(1, 2);
        var end = new Vector2(10, 20);

        IDrawable? lineDrawable = DrawableFactory.CreateShape(ShapeType.Line, start, end, style);
        IDrawable? arrowDrawable = DrawableFactory.CreateShape(ShapeType.Arrow, start, end, style);

        Assert.IsInstanceOfType<LineDrawable>(lineDrawable);
        var line = (LineDrawable)lineDrawable;
        Assert.AreEqual(start, line.Offset);
        Assert.AreEqual(end, line.EndPoint);
        Assert.AreEqual(Color.Green, line.StrokeColor);
        Assert.AreEqual(7, line.StrokeWidth);

        Assert.IsInstanceOfType<ArrowDrawable>(arrowDrawable);
        var arrow = (ArrowDrawable)arrowDrawable;
        Assert.AreEqual(start, arrow.Offset);
        Assert.AreEqual(end, arrow.EndPoint);
        Assert.AreEqual(Color.Green, arrow.StrokeColor);
        Assert.AreEqual(7, arrow.StrokeWidth);
    }

    [TestMethod]
    public void CreateShape_ShouldReturnNullForUnsupportedOrTooSmallShapes()
    {
        var style = new ShapeStyle(Color.Red, Color.Blue, 3);

        Assert.IsNull(DrawableFactory.CreateShape((ShapeType)999, Vector2.Zero, new Vector2(10), style));
        Assert.IsNull(DrawableFactory.CreateShape(ShapeType.Rectangle, Vector2.Zero, new Vector2(1, 10), style));
        Assert.IsNull(DrawableFactory.CreateShape(ShapeType.Ellipse, Vector2.Zero, new Vector2(10, 1), style));
        Assert.IsNull(DrawableFactory.CreateShape(ShapeType.Line, Vector2.Zero, new Vector2(1, 1), style));
        Assert.IsNull(DrawableFactory.CreateShape(ShapeType.Arrow, Vector2.Zero, new Vector2(1, 1), style));
    }

    [TestMethod]
    public void CreateTextBox_ShouldCreateTextDrawableFromBoundsAndStyle()
    {
        var style = new TextStyle(Color.Yellow, Color.DarkBlue, "Consolas", 18);

        TextDrawable? drawable = DrawableFactory.CreateTextBox(
            new Vector2(25.1f, 1.2f),
            new Vector2(10.4f, 9.6f),
            style);

        Assert.IsNotNull(drawable);
        Assert.AreEqual(new Vector2(10.4f, 1.2f), drawable.Offset);
        Assert.AreEqual(new Size(15, 9), drawable.Size);
        Assert.AreEqual(string.Empty, drawable.Text);
        Assert.AreEqual(Color.Yellow, drawable.Color);
        Assert.AreEqual(Color.DarkBlue, drawable.BackgroundColor);
        Assert.AreEqual("Consolas", drawable.FontFamily);
        Assert.AreEqual(18f, drawable.FontSize);
    }

    [TestMethod]
    public void CreateTextBox_ShouldReturnNullWhenEitherDimensionIsTooSmall()
    {
        var style = new TextStyle(Color.Yellow, Color.DarkBlue, "Consolas", 18);

        Assert.IsNull(DrawableFactory.CreateTextBox(Vector2.Zero, new Vector2(1, 5), style));
        Assert.IsNull(DrawableFactory.CreateTextBox(Vector2.Zero, new Vector2(5, 1), style));
    }
}
